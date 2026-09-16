using System;
using System.Collections.Generic;
using UnityEngine;

namespace OTA.Corridor.Annotation
{
    public interface IAnnotationAction
    {
        string Description { get; }
        void Undo();
        void Redo();
    }

    public class ReclassAction : IAnnotationAction
    {
        public int[] indices;
        public byte[] oldClasses;
        public byte newClass;
        public string desc;
        public string Description => desc;

        public ReclassAction(int[] idxs, byte[] olds, byte nClass, string d)
        {
            indices = idxs;
            oldClasses = olds;
            newClass = nClass;
            desc = d;
        }

        public void Undo()
        {
            if (AnnotationHistory.Instance != null)
            {
                AnnotationHistory.Instance.InvokeRevert(indices, oldClasses);
            }
        }

        public void Redo()
        {
            if (AnnotationHistory.Instance != null)
            {
                AnnotationHistory.Instance.InvokeReapply(indices, newClass);
            }
        }
    }

    public class TowerAddAction : IAnnotationAction
    {
        public TowerAnnotator annotator;
        public TowerAnnotation tower;
        public string Description => $"添加杆塔 {tower.towerNo}";

        public TowerAddAction(TowerAnnotator ann, TowerAnnotation t)
        {
            annotator = ann;
            tower = t;
        }

        public void Undo()
        {
            if (annotator != null && annotator.towers.Contains(tower))
            {
                annotator.towers.Remove(tower);
                if (annotator.activeTower == tower)
                {
                    annotator.activeTower = annotator.towers.Count > 0 ? annotator.towers[annotator.towers.Count - 1] : null;
                }
            }
        }

        public void Redo()
        {
            if (annotator != null && !annotator.towers.Contains(tower))
            {
                annotator.towers.Add(tower);
                annotator.activeTower = tower;
            }
        }
    }

    public class TowerModifyAction : IAnnotationAction
    {
        public TowerAnnotation tower;
        public float oldYaw, newYaw;
        public float oldHeadW, newHeadW;
        public float oldNominalH, newNominalH;
        public string Description => $"调整杆塔 {tower.towerNo} 几何参数";

        public TowerModifyAction(TowerAnnotation t, float oYaw, float nYaw, float oW, float nW, float oH, float nH)
        {
            tower = t;
            oldYaw = oYaw; newYaw = nYaw;
            oldHeadW = oW; newHeadW = nW;
            oldNominalH = oH; newNominalH = nH;
        }

        public void Undo()
        {
            tower.yawAngle = oldYaw;
            tower.nominalHeight = oldNominalH;
            tower.RecalculateBoxes(headWidth: oldHeadW);
        }

        public void Redo()
        {
            tower.yawAngle = newYaw;
            tower.nominalHeight = newNominalH;
            tower.RecalculateBoxes(headWidth: newHeadW);
        }
    }

    public class ConductorAddAction : IAnnotationAction
    {
        public ConductorAnnotator annotator;
        public ConductorAnnotation conductor;
        public string Description => $"添加导线 {conductor.phaseName}";

        public ConductorAddAction(ConductorAnnotator ann, ConductorAnnotation c)
        {
            annotator = ann;
            conductor = c;
        }

        public void Undo()
        {
            if (annotator != null && annotator.conductors.Contains(conductor))
            {
                annotator.conductors.Remove(conductor);
                if (annotator.activeConductor == conductor)
                {
                    annotator.activeConductor = annotator.conductors.Count > 0 ? annotator.conductors[annotator.conductors.Count - 1] : null;
                }
            }
        }

        public void Redo()
        {
            if (annotator != null && !annotator.conductors.Contains(conductor))
            {
                annotator.conductors.Add(conductor);
                annotator.activeConductor = conductor;
            }
        }
    }

    /// <summary>
    /// Transaction undo/redo stack for all annotation actions (towers, conductors, point classes).
    /// Supports Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z shortcuts. Decoupled from UI via OnGlobalToast.
    /// </summary>
    public class AnnotationHistory : MonoBehaviour
    {
        public static AnnotationHistory Instance { get; private set; }

        public static Action<string> OnGlobalToast;
        public static void ShowToast(string msg) => OnGlobalToast?.Invoke(msg);

        public delegate void RevertHandler(int[] indices, byte[] oldClasses);
        public delegate void ReapplyHandler(int[] indices, byte newClass);

        public event RevertHandler OnRevert;
        public event ReapplyHandler OnReapply;
        public event Action OnHistoryChanged;

        public void InvokeRevert(int[] indices, byte[] oldClasses) => OnRevert?.Invoke(indices, oldClasses);
        public void InvokeReapply(int[] indices, byte newClass) => OnReapply?.Invoke(indices, newClass);

        private Stack<IAnnotationAction> undoStack = new Stack<IAnnotationAction>();
        private Stack<IAnnotationAction> redoStack = new Stack<IAnnotationAction>();
        public int maxHistory = 50;

        public int UndoCount => undoStack.Count;
        public int RedoCount => redoStack.Count;

        void Awake()
        {
            Instance = this;
        }

        public void RecordAction(IAnnotationAction action)
        {
            if (action == null) return;
            undoStack.Push(action);
            if (undoStack.Count > maxHistory)
            {
                var arr = undoStack.ToArray();
                undoStack.Clear();
                for (int i = maxHistory - 1; i >= 0; i--) undoStack.Push(arr[i]);
            }
            redoStack.Clear();
            OnHistoryChanged?.Invoke();
            Debug.Log($"[AnnotationHistory] Recorded: {action.Description}");
        }

        public void RecordTransaction(int[] indices, byte[] oldClasses, byte newClass, string desc = "修改分类")
        {
            RecordAction(new ReclassAction(indices, oldClasses, newClass, desc));
        }

        public bool Undo()
        {
            if (ConductorAnnotator.Instance != null && ConductorAnnotator.Instance.HasPendingPoint)
            {
                ConductorAnnotator.Instance.CancelPending();
                ShowToast("已取消挂点 1");
                return true;
            }

            if (undoStack.Count == 0)
            {
                ShowToast("无可撤销操作");
                return false;
            }

            var act = undoStack.Pop();
            redoStack.Push(act);
            act.Undo();
            OnHistoryChanged?.Invoke();
            ShowToast($"已撤销: {act.Description}");
            Debug.Log($"[AnnotationHistory] Undo: {act.Description}");
            return true;
        }

        public bool Redo()
        {
            if (redoStack.Count == 0)
            {
                ShowToast("无可重做操作");
                return false;
            }

            var act = redoStack.Pop();
            undoStack.Push(act);
            act.Redo();
            OnHistoryChanged?.Invoke();
            ShowToast($"已重做: {act.Description}");
            Debug.Log($"[AnnotationHistory] Redo: {act.Description}");
            return true;
        }

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl)
            {
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        Redo();
                    }
                    else
                    {
                        Undo();
                    }
                }
                else if (Input.GetKeyDown(KeyCode.Y))
                {
                    Redo();
                }
            }
        }
    }
}
