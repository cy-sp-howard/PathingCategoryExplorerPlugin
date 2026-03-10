using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace BhModule.PathingCategoryExplorerPlugin
{
    public class PluginService
    {
        const string _pathingNamespace = "bh.community.pathing";
        ModuleSettings Settings => PathingCategoryExplorerPluginModule.Instance.Settings;
        ModuleManager _pathingModuleManager;
        readonly List<Action> _hookDisposeActions = [];
        Action<Control, bool> _setPathingNodeChecked;
        Func<Control, bool> _getPathingNodeChecked;
        Func<Control, bool> _getPathingNodeCheckable;
        Action<Control> _showAllCategories;
        Action<Control> _deselectAdjacentNodes;
        Action<Container> _disposeContainer;
        bool DependenciesMet => PathingCategoryExplorerPluginModule.InstanceManager.DependenciesMet;
        public void Upadate()
        {
            if (!DependenciesMet) return;
            if (_pathingModuleManager is null)
            {
                GetPathingModuleManager();
                BuildActions();
                HookCategoryContextMenu();
                HookTreeNodeBaseDispose();
            }
        }
        public void Unload()
        {
            if (_pathingModuleManager is null) return;
            _pathingModuleManager.ModuleDisabled -= OnPathingUnload;
            OnPathingUnload(this, EventArgs.Empty);
        }
        void OnPathingUnload(object sender, EventArgs e)
        {
            foreach (var dispose in _hookDisposeActions)
            {
                dispose();
            }
            _hookDisposeActions.Clear();
        }
        void BuildActions()
        {
            var pathingAssembly = Assembly.GetAssembly(_pathingModuleManager.ModuleInstance.GetType());
            var pathingCategoryNodeType = pathingAssembly.GetType("BhModule.Community.Pathing.UI.Controls.TreeNodes.PathingCategoryNode");
            var pathingNodeType = pathingCategoryNodeType.BaseType;

            ParameterExpression control = Expression.Parameter(typeof(Control));
            ParameterExpression isChecked = Expression.Parameter(typeof(bool));
            var ctrlAsPathingNode = Expression.TypeAs(control, pathingNodeType);
            var triggerCehcked = Expression.Call(
                ctrlAsPathingNode,
                pathingNodeType.GetMethod("CheckboxOnCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic),
                [ctrlAsPathingNode, Expression.New(typeof(CheckChangedEvent).GetConstructors()[0], [isChecked])]
                );
            var checkedDiff = Expression.NotEqual(Expression.Property(ctrlAsPathingNode, "Checked"), isChecked);
            var isPathingNodeThenSet = Expression.IfThen(
                Expression.And(Expression.TypeIs(control, pathingNodeType), checkedDiff),
                triggerCehcked
                );
            _setPathingNodeChecked = Expression.Lambda<Action<Control, bool>>(isPathingNodeThenSet, [control, isChecked]).Compile();

            var isPathingNodeTypeThenGet0 = Expression.Condition(
                Expression.TypeIs(control, pathingNodeType),
                Expression.Property(ctrlAsPathingNode, "Checked"),
                Expression.Constant(false)
                );
            _getPathingNodeChecked = Expression.Lambda<Func<Control, bool>>(isPathingNodeTypeThenGet0, [control]).Compile();

            var getCheckable = Expression.Property(ctrlAsPathingNode, "Checkable");
            var isPathingNodeTypeThenGet1 = Expression.Condition(
                Expression.TypeIs(control, pathingNodeType),
                getCheckable,
                Expression.Constant(false)
                );
            _getPathingNodeCheckable = Expression.Lambda<Func<Control, bool>>(isPathingNodeTypeThenGet1, [control]).Compile();

            var showAll = Expression.Call(
                Expression.TypeAs(control, pathingCategoryNodeType),
                pathingCategoryNodeType.GetMethod("ShowAllSkippedCategories_LeftMouseButtonReleased", BindingFlags.Instance | BindingFlags.NonPublic),
                [control, Expression.Constant(new MouseEventArgs(MouseEventType.LeftMouseButtonReleased))]
                );
            var isPathingCategoryNodeThenShow = Expression.IfThen(
                Expression.TypeIs(control, pathingCategoryNodeType),
                showAll
                );
            _showAllCategories = Expression.Lambda<Action<Control>>(isPathingCategoryNodeThenShow, [control]).Compile();

            var deselectAdjacentNodesExcept = Expression.Call(
               ctrlAsPathingNode,
               pathingNodeType.GetMethod("DeselectAdjacentNodesExcept"),
               [ctrlAsPathingNode]
               );
            var isPathingNodeTypeThenDeslect = Expression.IfThen(
                Expression.TypeIs(control, pathingNodeType),
                deselectAdjacentNodesExcept
                );
            _deselectAdjacentNodes = Expression.Lambda<Action<Control>>(isPathingNodeTypeThenDeslect, [control]).Compile();

            var disposeContainer = typeof(Container).GetMethod("DisposeControl", BindingFlags.NonPublic | BindingFlags.Instance);
            var dm = new DynamicMethod("DisposeContainer", null, [typeof(Container)], typeof(Container), true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, disposeContainer);
            il.Emit(OpCodes.Ret);
            _disposeContainer = dm.CreateDelegate<Action<Container>>();
        }
        void GetPathingModuleManager()
        {
            _pathingModuleManager = GameService.Module.Modules.FirstOrDefault(m => m.Manifest.Namespace == _pathingNamespace);
            _pathingModuleManager.ModuleDisabled += OnPathingUnload;
        }
        void HookCategoryContextMenu()
        {
            var pathingAssembly = Assembly.GetAssembly(_pathingModuleManager.ModuleInstance.GetType());
            var pathingNodeType = pathingAssembly.GetType("BhModule.Community.Pathing.UI.Controls.TreeNodes.PathingNode");
            var BuildDeselectAdjacentNodesMethodInfo = pathingNodeType.GetMethod("BuildDeselectAdjacentNodes", BindingFlags.NonPublic | BindingFlags.Instance);
            var hook = new Hook(BuildDeselectAdjacentNodesMethodInfo, BuildContextMenu);
            _hookDisposeActions.Add(() => hook.Dispose());
        }
        void HookTreeNodeBaseDispose()
        {
            var pathingAssembly = Assembly.GetAssembly(_pathingModuleManager.ModuleInstance.GetType());
            var pathingNodeType = pathingAssembly.GetType("BhModule.Community.Pathing.UI.Controls.TreeNodes.TreeNodeBase");
            var DisposeControlMethodInfo = pathingNodeType.GetMethod("DisposeControl", BindingFlags.NonPublic | BindingFlags.Instance);
            var hook = new Hook(DisposeControlMethodInfo, DisposeTreeNodeBase, Settings.FixNodeExpansionBug.Value);
            void applyHook(object sender, ValueChangedEventArgs<bool> e)
            {
                if (e.NewValue) hook.Apply();
                else hook.Undo();
            }
            Settings.FixNodeExpansionBug.SettingChanged += applyHook;
            _hookDisposeActions.Add(() =>
            {
                Settings.FixNodeExpansionBug.SettingChanged -= applyHook;
                hook.Dispose();
            });
        }
        void DisposeTreeNodeBase(Action<object> dispose, object instance)
        {
            if (instance is Container container) _disposeContainer(container);
            dispose(instance);
        }
        void BuildContextMenu(Action<object> BuildDeselectAdjacentNodes, object instance)
        {
            if (instance is Container pathingNode)
            {
                BuildDeselectAdjacentNodes(pathingNode);
                if (Settings.AddSelectRecursively.Value)
                {
                    var stripItem = new ContextMenuStripItem("Select Recursively")
                    {
                        Parent = pathingNode.Menu
                    };
                    stripItem.Click += (_, _) =>
                    {
                        var restoreParentChecked = ActiveAllParentsAndSelf(pathingNode);
                        SelectRecursively(pathingNode, true);
                        restoreParentChecked?.Invoke();
                    };
                }
                if (Settings.AddDeselectRecursively.Value)
                {
                    var stripItem = new ContextMenuStripItem("Deselect Recursively")
                    {
                        Parent = pathingNode.Menu
                    };
                    stripItem.Click += (_, _) =>
                    {
                        SelectRecursively(pathingNode, false);
                    };
                }
                if (Settings.AddDeselectAllOthers.Value)
                {
                    var stripItem = new ContextMenuStripItem("Deselect All Others")
                    {
                        Parent = pathingNode.Menu
                    };
                    stripItem.Click += (_, _) =>
                    {
                        ActiveAllParentsAndSelf(pathingNode);
                        DeselectAllOthers(pathingNode);
                    };
                }
            }
        }
        void SelectRecursively(Container pathingNode, bool checkedValue)
        {
            var pathingNodeType = pathingNode.GetType();
            _setPathingNodeChecked?.Invoke(pathingNode, checkedValue);
            _showAllCategories?.Invoke(pathingNode);
            foreach (var child in pathingNode.Children)
            {
                if (!pathingNodeType.Equals(child.GetType())) continue;
                if (_getPathingNodeCheckable?.Invoke(child) != true) continue;
                _setPathingNodeChecked?.Invoke(child, checkedValue);
                if (child is Container childContainer)
                {
                    SelectRecursively(childContainer, checkedValue);
                }
            }
        }
        Action ActiveAllParentsAndSelf(Container pathingNode)
        {
            Action restoreActions = null;
            var pathingNodeType = pathingNode.GetType();
            List<Container> nodes = [pathingNode];
            while (nodes.Last().Parent.GetType() == pathingNodeType)
            {
                nodes.Add(nodes.Last().Parent);
            }
            nodes.Reverse();
            foreach (var node in nodes)
            {
                var oldVal = _getPathingNodeChecked?.Invoke(node);
                if (node != pathingNode && oldVal != true) restoreActions += () => { _setPathingNodeChecked?.Invoke(node, false); };
                _setPathingNodeChecked?.Invoke(node, true);
            }
            return restoreActions;
        }
        void DeselectAllOthers(Container pathingNode)
        {
            _deselectAdjacentNodes(pathingNode);
            if (pathingNode.Parent.GetType() != pathingNode.GetType()) return;
            DeselectAllOthers(pathingNode.Parent);
        }
    }
}
