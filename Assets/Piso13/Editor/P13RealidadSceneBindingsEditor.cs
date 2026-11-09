using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Piso13.Realidad;

namespace Piso13.Editor
{
    [CustomEditor(typeof(P13RealidadSceneBindings))]
    public sealed class P13RealidadSceneBindingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var b = (P13RealidadSceneBindings)target;
            root.Add(new HelpBox("Escena persistente: edita Transform, luces, materiales y marcadores sin entrar en Play. No regeneres el escenario. Mantén los volúmenes alineados a los ejes (AABB).", HelpBoxMessageType.Info));
            var status = new HelpBox("", HelpBoxMessageType.Info);
            System.Action validate = () =>
            {
                var errors = b.ValidateScene();
                status.text = errors.Count == 0 ? "Referencias válidas." : string.Join("\n", errors);
                status.messageType = errors.Count == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
            };
            root.Add(new Button(validate) { text = "Validar escena" });
            root.Add(new Button(() =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || b.WorldRoot == null) return;
                Undo.RecordObject(b, "Actualizar marcadores");
                var so = new SerializedObject(b);
                var array = so.FindProperty("_spatial");
                var markers = b.WorldRoot.GetComponentsInChildren<P13RealidadSpatialAuthoring>(true);
                array.arraySize = markers.Length;
                for (int i = 0; i < markers.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
                so.ApplyModifiedProperties();
                validate();
            }) { text = "Actualizar lista tras añadir marcadores" });
            root.Add(status);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            validate();
            return root;
        }
    }
}
