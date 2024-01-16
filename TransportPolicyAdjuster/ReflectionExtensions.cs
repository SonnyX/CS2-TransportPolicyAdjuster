using System.Reflection;

namespace TransportPolicyAdjuster
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Uses reflection to get the value of a member of an object.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <returns>Value of the member of the origial object.</returns>
        /// <exception cref="System.Exception">Exception for not finding the specified member name within the object.</exception>
        public static T GetMemberValue<T>(this object obj, string memberName)
        {
            var memInf = GetMemberInfo(obj, memberName);

            if (memInf is PropertyInfo)
            {
                return (T)memInf.As<PropertyInfo>().GetValue(obj, null);
            }

            if (memInf is FieldInfo)
            {
                return (T) memInf.As<FieldInfo>().GetValue(obj);
            }

            throw new System.Exception($"{nameof(ReflectionExtensions)}: Couldn't find member name: {memberName}!");
        }

        /// <summary>
        /// Uses Reflection to Set to value of a member of an object.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <param name="newValue">New value to be set.</param>
        /// <returns>Returns old value.</returns>
        /// <exception cref="System.Exception">Exception thrown if member name is not found on object.</exception>
        public static object SetMemberValue(this object obj, string memberName, object newValue)
        {
            var memInf = GetMemberInfo(obj, memberName);

            var oldValue = obj.GetMemberValue<object>(memberName);
            if (memInf is PropertyInfo)
            {
                memInf.As<PropertyInfo>().SetValue(obj, newValue, null);
            }
            else if (memInf is FieldInfo)
            {
                memInf.As<FieldInfo>().SetValue(obj, newValue);
            }
            else
            {
                throw new System.Exception($"{nameof(ReflectionExtensions)}: Couldn't find member name {memberName}! ");
            }

            return oldValue;
        }

        /// <summary>
        /// Uses reflection to get member info.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <returns>Member info.</returns>
        private static MemberInfo GetMemberInfo(object obj, string memberName)
        {
            var prps = new System.Collections.Generic.List<PropertyInfo>
            {
                obj.GetType().GetProperty(
                    memberName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy),
            };

            prps = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(prps, i => i is not null));
            if (prps.Count != 0)
            {
                return prps[0];
            }

            var flds = new System.Collections.Generic.List<FieldInfo>
            {
                obj.GetType().GetField(
                    memberName,
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy),
            };

            flds = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(flds, i => i is not null));
            if (flds.Count != 0)
            {
                return flds[0];
            }

            return null;
        }

        [System.Diagnostics.DebuggerHidden]
        private static T As<T>(this object obj)
        {
            return (T)obj;
        }
    }
}