﻿using System;
using System.Collections.Generic;
using Assets.Editor.BehaviorTreeViewEditor;
using Assets.Scripts.AI;
using Assets.Scripts.AI.Behavior_Logger;
using UniRx;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Assets.Editor
{
    public class TreeDebuggerWindow : EditorWindow
    {
        private static Vector2 BehaviorLogRectSize = new Vector2(120, 120);
        private RectOffset MinimumMargins;

        private GUIStyle TreeStyle = new GUIStyle();

        private void OnEnable()
        {
            Initialize();
        }

        private CompositeDisposable Disposables = new CompositeDisposable();

        public StringReactiveProperty ManagerName = new StringReactiveProperty();

        /// <summary>
        /// Behavior IDs to track current behaviors being watched.
        /// </summary>
        public Dictionary<int, BehaviorLogDrawer> LogDrawers = new Dictionary<int, BehaviorLogDrawer>();

        Rect TopToolbarRect
        {
            get { return new Rect(10f, 5f, position.width - 40f, 30f); }
        }

        Rect TreeDrawArea
        {
            get { return new Rect(6f, 20f, position.width - 30f, position.height - 40f); }
        }

        [MenuItem("Behavior Tree/Debugger")]
        public static TreeDebuggerWindow ShowWindow()
        {
            var window = GetWindow<TreeDebuggerWindow>();
            window.Focus();
            window.Repaint();
            return window;
        }

        private void OnGUI()
        {
            if (!Initialized) Initialize();

            using (new EditorGUILayout.VerticalScope())
            {
                TopToolbar(TopToolbarRect);
                TreeLogArea(TreeDrawArea);
            }
        }

        private bool Initialized = false;
        private void Initialize()
        {
            BehaviorLogRectSize = new Vector2(120, 120);
            MinimumMargins = new RectOffset(15, 15, 30, 30);
            LogDrawers = new Dictionary<int, BehaviorLogDrawer>();
            rowTotalDrawn = new Dictionary<int, int>();
            parents = new HashSet<BehaviorTreeElement>();
            this.autoRepaintOnSceneChange = true;

            TreeStyle.margin = MinimumMargins;
            //this.Style.margin = MinimumMargins;
            Initialized = true;
        }

        GenericMenu ManagerSelectMenu = new GenericMenu();
        private void TopToolbar(Rect rect)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(5);
                string dropDownName = "Manager To Debug";
                if (ManagerName.Value != "") dropDownName = ManagerName.Value;

                if(EditorGUILayout.DropdownButton(new GUIContent(dropDownName, "Change visible debugger"), FocusType.Passive, GUILayout.Height(30)))
                {
                    ManagerSelectMenu.CreateManagerMenu(OnManagerSelected);
                    ManagerSelectMenu.ShowAsContext();
                }
            }
        }

        private void OnManagerSelected(object name)
        {
            ManagerName.SetValueAndForceNotify((string)name);
            Initialize();
        }

        private Dictionary<int, int> rowTotalDrawn = new Dictionary<int, int>();
        private HashSet<BehaviorTreeElement> parents = new HashSet<BehaviorTreeElement>();
        bool parentPaddingSet = false;

        private void TreeLogArea(Rect rect)
        {
            ObservableBehaviorLogger.Listener
                .Where(x => x.LoggerName == ManagerName.Value &&
                       x.State.HasChildren
                        )
                .Do(x =>
                {
                    //keep a single drawer per ID value
                    if (!LogDrawers.ContainsKey(x.BehaviorID))
                    {
                        //keep only the parents. Parents are responsible for drawing their children.
                        LogDrawers.Add(x.BehaviorID, 
                            new BehaviorLogDrawer(x.LoggerName, x.BehaviorID, new Rect(10,10,10,10)));
                    }
                })
                .Subscribe();
            GUI.BeginGroup(rect,TreeStyle);
            DrawAllLogDrawers();
            GUI.EndGroup();
        }

        private void DrawAllLogDrawers()
        {

            var parentsDepthSorted = LogDrawers.Values.OrderByDescending(x => x.Entry.State.Depth);
            
            foreach(var parentDrawer in parentsDepthSorted)
            {
                //Should be called in sorted order, from bottom to top.
                //this allows lower depth parents to know the correct spacing of their children
                parentDrawer.DrawBehaviorWithAllChildren();
            }
        }

        private void SetAllParentsMarginsDepthFirst()
        {
            var allParents = from parent in parents
                               select parent;

            if (allParents.Count() == 0)
            {
                Debug.LogWarning("no parents found!");
                return;
            }
            for (int depth = rowTotalDrawn.Keys.Max(); depth >= -1; --depth)
            {
                var currentDepthParents = from parent in allParents
                                          where parent.Depth == depth
                                          select parent;

                Debug.Log("Depth: " + depth);
                foreach (var parent in currentDepthParents)
                {
                    int paddingLeft = 0;
                    int paddingRight = 0;
                    int childNum = 0;
                    foreach (var child in parent.Children)
                    {
                        if(childNum > 0)
                        {
                            paddingLeft += (int)BehaviorLogRectSize.x / 2;
                            paddingRight += (int)BehaviorLogRectSize.x / 2;
                            paddingLeft += LogDrawers[child.ID].TotalOffset.left;
                            paddingRight += LogDrawers[child.ID].TotalOffset.right;
                        }
                        ++childNum;
                    }
                    //paddingLeft -= (int)BehaviorLogRectSize.x/2;
                    LogDrawers[parent.ID].TotalOffset = new RectOffset(paddingLeft+MinimumMargins.left/parent.Children.Count, 
                                                                       paddingRight, MinimumMargins.top, MinimumMargins.bottom);
                    //LogDrawers[parent.ID].Initialize();
                }
            }
            parentPaddingSet = true;
        }

        private void OnDestroy()
        {
            Disposables.Clear();
        }
    }
}
