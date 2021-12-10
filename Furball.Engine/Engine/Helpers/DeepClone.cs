using System;
using System.Collections.Generic;
using System.Reflection;
using Furball.Engine.Engine.Helpers.ArrayExtensions;
using JetBrains.Annotations;

namespace Furball.Engine.Engine.Helpers {
    public static class ObjectExtensions {
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsPrimitive(this Type type) {
            if (type == typeof(string)) return true;

            return type.IsValueType & type.IsPrimitive;
        }

        /// <summary>
        /// Does a deep copy of an object
        /// </summary>
        /// <param name="originalObject">The original object</param>
        /// <returns>The cloned object</returns>
        [CanBeNull]
        public static object Copy(this object originalObject) => InternalCopy(originalObject, new Dictionary<object, object>(new ReferenceEqualityComparer()));
        [CanBeNull]
        private static object InternalCopy(object originalObject, IDictionary<object, object> visited) {
            if (originalObject == null) return null;

            Type typeToReflect = originalObject.GetType();

            if (IsPrimitive(typeToReflect)) return originalObject;

            if (visited.ContainsKey(originalObject)) return visited[originalObject];

            if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;

            object cloneObject = CloneMethod.Invoke(originalObject, null);

            if (typeToReflect.IsArray) {
                Type arrayType = typeToReflect.GetElementType();

                if (IsPrimitive(arrayType) == false) {
                    Array clonedArray = (Array)cloneObject;
                    clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                }

            }
            visited.Add(originalObject, cloneObject);

            CopyFields(originalObject, visited, cloneObject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);

            return cloneObject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect) {
            if (typeToReflect.BaseType != null) {
                RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
                CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
            }
        }

        private static void CopyFields(
            object                originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect,
            BindingFlags          bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
            Func<FieldInfo, bool> filter       = null
        ) {
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags)) {
                if (filter != null && filter(fieldInfo) == false) continue;
                if (IsPrimitive(fieldInfo.FieldType)) continue;

                object originalFieldValue = fieldInfo.GetValue(originalObject);
                object clonedFieldValue   = InternalCopy(originalFieldValue, visited);

                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }
        }
        public static T Copy <T>(this T original) => (T)Copy((object)original);
    }

    public class ReferenceEqualityComparer : EqualityComparer<object> {
        public override bool Equals(object x, object y) => ReferenceEquals(x, y);
        public override int GetHashCode(object obj) {
            if (obj == null) return 0;
            return obj.GetHashCode();
        }
    }

    namespace ArrayExtensions {
        public static class ArrayExtensions {
            public static void ForEach(this Array array, Action<Array, int[]> action) {
                if (array.LongLength == 0) return;

                ArrayTraverse walker = new(array);

                do {
                    action(array, walker.Position);
                } while (walker.Step());
            }
        }

        internal class ArrayTraverse {
            public  int[] Position;
            private int[] maxLengths;

            public ArrayTraverse(Array array) {
                this.maxLengths = new int[array.Rank];

                for (int i = 0; i < array.Rank; ++i)
                    this.maxLengths[i] = array.GetLength(i) - 1;

                this.Position = new int[array.Rank];
            }

            public bool Step() {
                for (int i = 0; i < this.Position.Length; ++i)
                    if (this.Position[i] < this.maxLengths[i]) {
                        this.Position[i]++;

                        for (int j = 0; j < i; j++)
                            this.Position[j] = 0;

                        return true;
                    }
                return false;
            }
        }
    }

}
