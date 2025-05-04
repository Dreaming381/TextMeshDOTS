//using TextMeshDOTS.Authoring;
//using UnityEditor;
//using UnityEditor.UIElements;
//using UnityEngine.UIElements;

//namespace TextMeshDOTS
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
//            //To-do: does not work when TextRendererAuthoring.fontCollectionAsset is not set yet. 
//            fonts.choices = ((TextRendererAuthoring)this.target).fontCollectionAsset.fontFamilies;
//            myInspector.Add(container);

//            return myInspector;
//        }
//    }
//}
