using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Furball.Engine.Engine.Helpers {
    public class ObjectHelper {
        public static IEnumerable<T> GetEnumerableOfType <T>(params object[] constructorArgs) where T : class, IComparable<T> {
            List<T> objects = Assembly.GetAssembly(typeof(T)).GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T)))
                                      .Select(type => (T)Activator.CreateInstance(type, constructorArgs)).ToList();
            objects.Sort();
            return objects;
        }
    }
}
