namespace Utils.Expressions
{
    static public class ObjectDynamicExtension
    {
        /// <summary>
        /// Execute the code as an instance as a context
        /// </summary>
        /// <typeparam name = "T"> returns the result type </ typeparam>
        /// <param name = "instance"> execute the object's code as the context ($ 0 code, $ 0 can be omitted) </param>
        /// <param name = "code"> execute the code </ param>
        /// <param name = "namespaces"> import namespace </ param>
        /// <param name = "objects"> parameter object ($ 1 for the first object; $ 2 for the second object) </param>
        /// <returns> </returns> 

        static public T E<T>(this object instance, string code, string[] namespaces, params object[] objects)
            where T : class
        {
            return ExpressionParser.Exec<T>(instance, code, namespaces, objects);
        }
        /// <summary>
        /// Execute the code with instance as the context
        /// </summary>
        /// <typeparam name = "T"> returned result type </typeparam>
        /// <param name = "instance"> execute the code with this object as the context ($ 0 in code, $ 0 can be omitted) </param>
        /// <param name = "code"> executed code </param>
        /// <param name = "objects"> parameter object ($ 1 in code that the first object; $ 2 that the second object ....) </param>
        /// <returns> </returns>

        static public T E<T>(this object instance, string code, params object[] objects)
        {
            return ExpressionParser.Exec<T>(instance, code, null, objects);
        }

        /// <summary>
        /// Execute the code as an instance as a context
        /// </summary>
        /// <param name = "instance"> execute the object's code as the context ($ 0 code, $ 0 can be omitted) </param>
        /// <param name = "code"> execute the code </param>
        /// <param name = "namespaces"> import namespace </param>
        /// <param name = "objects"> parameter object ($ 1 for the first object; $ 2 for the second object) </param>
        /// <returns> </returns>
        static public object E(this object instance, string code, string[] namespaces, params object[] objects)
        {
            return ExpressionParser.Exec(instance, code, namespaces, objects);
        }

        /// <summary>
        /// Execute the code with instance as the context
        /// </summary>
        /// <param name = "instance"> execute the code with this object as the context ($ 0 in code, $ 0 can be omitted) </param>
        /// <param name = "code"> executed code </param>
        /// <param name = "objects"> parameter object ($ 1 in code that the first object; $ 2 that the second object ....) </param>
        /// <returns> </returns>
        static public object E(this object instance, string code, params object[] objects)
        {
            return ExpressionParser.Exec(instance, code, null, objects);
        }
    }
}