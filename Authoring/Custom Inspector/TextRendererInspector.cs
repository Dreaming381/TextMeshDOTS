//using TextMeshDOTS.Authoring;
//using UnityEditor;
//using UnityEditor.UIElements;
//using UnityEngine.UIElements;

//namespace TextmeshDOTS
//{
//    [CustomEditor(typeof(TextRendererAuthoring))]
//    public class TextRendererInspector : Editor
//    {
//        public VisualTreeAsset visualTreeAsset;

//        public override VisualElement CreateInspectorGUI()
//        {
//            VisualElement myInspector = new VisualElement();
//            var container = visualTreeAsset.Instantiate();
//            var fonts = container.Q<DropdownField>();
//            fonts.choices = ((TextRendererAuthoring)this.target).fontCollectionAsset.fontFamilies;
//            myInspector.Add(container);

//            return myInspector;
//        }
//    }
//}
