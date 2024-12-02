using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace OddRhythms
{
    [CustomEditor(typeof(OddRhythms))]
    [CanEditMultipleObjects]
    class SeedObjectEditor : Editor
    {
        SerializedProperty seed, prevObject, favObject, test;

        public void OnEnable()
        {
                       seed = serializedObject.FindProperty("seed");

            prevObject = serializedObject.FindProperty("prevObject");
            favObject = serializedObject.FindProperty("favObject");
            test = serializedObject.FindProperty("test");
            /*
            if (test is null)
            {
                Debug.Log("Couldn't find test");
            }
            if (prevObject is null)
            {
                Debug.Log("Couldn't find prevObject");
            }
            if (favObject is null)
            {
                Debug.Log("Couldn't find favObject");
            }
            */
        }

        public override void OnInspectorGUI()
        {
            OddRhythms t = (target as OddRhythms);

            serializedObject.Update();

            if (EditorGUILayout.LinkButton("Generate Seed"))
            {
                t.GetNewSeed();
                //Debug.Log("Generating Seed");
            }

            if (seed is null)
            {
                //Debug.Log("Seed is null");
            }
            else
            {
                //SerializedProperty test;
                EditorGUILayout.LabelField(new GUIContent("Actual Seed"));
                EditorGUILayout.PropertyField(seed);//, new GUIContent("Actual Seed"));
                EditorGUILayout.LabelField("Non-Favorites");
            }
            /*
            if (test is null)
            {
                Debug.Log("Test is null");
            }
            else
            {
              //  EditorGUILayout.PropertyField(test);
            }
            */
            for (int i = 0; i < prevObject.arraySize; ++i)
            {
                if (!(t.prevObject[i] is null) && t.prevObject[i].isValid)
                {
                    EditorGUILayout.PropertyField(prevObject.GetArrayElementAtIndex(i));
                    EditorGUILayout.BeginHorizontal();
                    if (EditorGUILayout.LinkButton("Choose"))
                    {
                        t.SetAsSeed(i, false);
                    }

                    if (EditorGUILayout.LinkButton("Toggle Favorite"))
                    {
                        t.SetFav(i);
                    }
                    //               if (t.prevObject[i].SeedIsReady(t.prevObject[i].valence, t.prevObject[i].energy))
                    //               {
                    if (EditorGUILayout.LinkButton("Play/Stop"))
                    {
                        t.PlayOrStop(i, true);
                    }
                    //               }
                    //              else
                    //            {
                    //              EditorGUILayout.LabelField("Filling seeds");
                    //        }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.LabelField(new GUIContent("Favorites"), EditorStyles.boldLabel);
            
            for (int i = 0; i < favObject.arraySize; ++i)
            {
                if (!(t.favObject[i] is null) && t.favObject[i].isValid)
                {
                    EditorGUILayout.PropertyField(favObject.GetArrayElementAtIndex(i));
                    EditorGUILayout.BeginHorizontal();

                    if (EditorGUILayout.LinkButton("Choose"))
                    {
                        t.SetAsSeed(i, true);
                    }

                    if (EditorGUILayout.LinkButton("Toggle Favorite"))
                    {
                        //Debug.Log("Remove: " + i);
                        t.RemFav(i);
                    }
                    //               if (t.favObject[i].IsReady)
                    //             {
                    if (EditorGUILayout.LinkButton("Play/Stop"))
                    {
                        t.PlayOrStop(i, false);
                    }
                    //    }
                    //           else
                    //         {
                    //           EditorGUILayout.LabelField("Filling seeds");
                    //     }
                    EditorGUILayout.EndHorizontal();
                }
            }
            //            EditorGUILayout.PropertyField(prevObject);
//            EditorGUILayout.PropertyField(favObject);
            serializedObject.ApplyModifiedProperties();

        }
    }
    
    [CustomPropertyDrawer(typeof(SeedObject))]
    class SeedDrawer : PropertyDrawer
    {
        public override UnityEngine.UIElements.VisualElement CreatePropertyGUI(SerializedProperty property)
        {

            return base.CreatePropertyGUI(property);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            /*
            var t = GetPropertyInstance(property) as SeedObject;
            if (!(t is null))
            {
                Debug.Log("target is of type " + t.GetType());
            }
            if (property.serializedObject.hasModifiedProperties)
            {
                Debug.Log("Something changed!");
            }
            
            Debug.Log("List of Properties for " + property.name);
            while (property.Next(true))
                {
                Debug.Log(property.name);
            }
            property.Reset();
            */
            SerializedProperty test = property.FindPropertyRelative("seed");
            EditorGUI.BeginProperty(position, label, property);
            Rect startPosition = position;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Labels"));
            EditorGUI.LabelField(new Rect(position.x, position.y, 80, EditorGUIUtility.singleLineHeight), new GUIContent("Seed"));
            EditorGUI.LabelField(new Rect(position.x + 85, position.y, 40, EditorGUIUtility.singleLineHeight), new GUIContent("Energy"));
            EditorGUI.LabelField(new Rect(position.x + 130, position.y, 40, EditorGUIUtility.singleLineHeight), new GUIContent("Valence"));
            EditorGUI.LabelField(new Rect(position.x + 175, position.y, position.width - 175, EditorGUIUtility.singleLineHeight), new GUIContent("Version"));
            position = startPosition;
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            Rect noteRect;
            /*
            if (test is null)
            {
                Debug.Log("Test is Null");
            }
            */
 //           Debug.Log("Is this refreshing?");
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Seed"));
            Rect seedRect = new Rect(position.x, position.y, 80, EditorGUIUtility.singleLineHeight);
//            Debug.Log("1");
            Rect arrRect = new Rect(position.x + 85, position.y, 40, EditorGUIUtility.singleLineHeight);
//            Debug.Log("2");
            Rect valRect = new Rect(position.x + 130, position.y, 40, EditorGUIUtility.singleLineHeight);
//            Debug.Log("3");
            Rect buttonRect = new Rect(position.x + 175, position.y, position.width - 175, EditorGUIUtility.singleLineHeight);
//            Debug.Log("4");

            if (!(test is null))
            {
                noteRect = new Rect(startPosition.x, position.y + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("seed")) + EditorGUIUtility.standardVerticalSpacing, startPosition.width, EditorGUI.GetPropertyHeight(property.FindPropertyRelative("note")));
                // * 3 + EditorGUIUtility.standardVerticalSpacing * 2);
//            Debug.Log("5");

                EditorGUI.PropertyField(seedRect, property.FindPropertyRelative("seed"), GUIContent.none);
            }
            else
            {
                noteRect = new Rect(startPosition);
            }
//            Debug.Log("6");
            EditorGUI.Slider(arrRect, property.FindPropertyRelative("_energy"), 1, 5, GUIContent.none);
//            Debug.Log("7");
            EditorGUI.Slider(valRect, property.FindPropertyRelative("_valence"), 1, 5, GUIContent.none);
            //            Debug.Log("8");
            EditorGUI.PropertyField(buttonRect, property.FindPropertyRelative("version"), GUIContent.none);

            position = EditorGUI.PrefixLabel(noteRect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("Note"));

            EditorGUI.PropertyField(new Rect(position.x, position.y - EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, position.height), property.FindPropertyRelative("note"), GUIContent.none);
            
//            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
/*            
                SeedObject s = property as object as SeedObject;
                if (s is null)
                {
                    Debug.Log("Failure to convert");
                Debug.Log("Property Type: " + property.type);
                }
                else
                {
                Debug.Log("Sucess to convert!");
//                    s.TestValidate(newVal, newAro);
                }
                
            OddRhythms r = property.serializedObject.targetObject as OddRhythms;
            if (r is null)
            {
                Debug.Log("Failure to convert to twp");
            }
            else
            {
                //                    s.TestValidate(newVal, newAro);
            }
*/
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineCount = 4.5f;
            return EditorGUIUtility.singleLineHeight * lineCount + EditorGUIUtility.standardVerticalSpacing * (lineCount - 1);
            //            return base.GetPropertyHeight(property, label);
        }

        public System.Object GetPropertyInstance(SerializedProperty p)
        {
            string path = p.propertyPath;

            System.Object retval = p.serializedObject.targetObject;
            Type type = retval.GetType();

            var fieldNames = path.Split('.');
            for (int i = 0; i < fieldNames.Length; ++i)
            {
                System.Reflection.FieldInfo info = type.GetField(fieldNames[i]);
                if (info == null)
                    break;

                retval = info.GetValue(retval);
                type = info.FieldType;
            }

            return retval;
        }
    }//*/
}
