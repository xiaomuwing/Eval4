﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;

namespace Eval4.Core
{

    // Expr is an implementation for IHasValue.
    // in Evaluator we should not be using Expr but only IHasValue
    public abstract class Expr : IHasValue, IObserver
    {
        internal List<Subscription> mSubscribedBy = new List<Subscription>();
        internal List<Dependency> mDependencies = new List<Dependency>();
        internal List<IDisposable> mSubscribedTo = new List<IDisposable>();
        protected bool mModified;

        public Expr(Dependency p0, params Dependency[] parameters)
        {
            mModified = true;
            AddDependency(p0);
            if (parameters != null)
            {
                foreach (Dependency p1 in parameters)
                {
                    AddDependency(p1);
                }
            }

        }

        protected void AddDependency(Dependency p0)
        {
            if (p0 != null)
            {
                mDependencies.Add(p0);
                if (p0.Value != null) mSubscribedTo.Add(p0.Value.Subscribe(this));
            }
        }

        public abstract object ObjectValue { get; }

        public IDisposable Subscribe(IObserver observer)
        {
            var result = new Subscription(this, observer);
            mSubscribedBy.Add(result);
            return result;
        }

        public abstract Type ValueType { get; }

        public abstract string ShortName { get; }

        void IObserver.OnValueChanged()
        {
            RaiseValueChanged();
        }

        protected void RaiseValueChanged()
        {
            mModified = true;
            foreach (var subscription in mSubscribedBy)
            {
                subscription.mObserver.OnValueChanged();
            }
        }

        public IEnumerable<Dependency> Dependencies
        {
            get
            {
                return this.mDependencies.ToArray();
            }
        }
    }

    public class Subscription : IDisposable
    {
        internal Expr mSource;
        internal IObserver mObserver;

        public Subscription(Expr source, IObserver observer)
        {
            mSource = source;
            mObserver = observer;
        }
        public void Dispose()
        {
            mSource.mSubscribedTo.Remove(this);
        }

    }

    public abstract class Expr<T> : Expr, IHasValue<T>
    {
        private T mValue;

        public abstract T Value { get; }


        public Expr(Dependency p0, params Dependency[] parameters)
            : base(p0, parameters)
        {
        }

        public override object ObjectValue
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return Value; }
        }

        public override Type ValueType
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return typeof(T); }
        }

        T IHasValue<T>.Value
        {
            get
            {
                //  if (mModified)
                //  {
                mModified = false;
                mValue = this.Value;
                //    }
                return mValue;
            }
        }
    }

    internal class ConstantExpr<T> : Expr<T>
    {
        private T mValue;

        public ConstantExpr(T value)
            : base(null)
        {
            mValue = value;
        }

        //public event ValueChangedEventHandler ValueChanged;

        public override string ShortName
        {
            get { return "Constant"; }
        }

        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mValue; }
        }
    }

    public class ArrayBuilder<T> : Expr<T[]>
    {
        private IHasValue[] mEntries;
        private Delegate[] mCasts;
        private T[] mResult;

        public ArrayBuilder(IHasValue[] entries, object[] casts)
            : base(null, Dependency.Group("entries", entries))
        {
            mEntries = entries;
            mCasts = casts.Cast<Delegate>().ToArray();
            mResult = new T[entries.Length];
        }

        public override T[] Value
        {
            get
            {
                for (int i = 0; i < mEntries.Length; i++)
                {
                    var val = mEntries[i].ObjectValue;
                    var cast = mCasts[i];
                    mResult[i] = (cast == null ? (T)val : (T)cast.DynamicInvoke(val));
                }
                return mResult;
            }
        }

        public override string ShortName
        {
            get
            {
                return "ArrayBuilder";
            }
        }
    }


    public class CallMethodExpr<T> : Expr<T>
    {
        Func<T> mValueDelegate;
        private IHasValue mBaseObject;
        private IHasValue withEventsField_mBaseValue;
        private object mBaseValueObject;
        private MemberInfo mMethod;
        private IHasValue[] mPreparedParams;

        private object[] mParamValues;
        private System.Type mResultSystemType;
        private string mShortName;

        public CallMethodExpr(IHasValue baseObject, MemberInfo method, List<IHasValue> @params, object[] casts, string shortName)
            : base(new Dependency("baseobject", baseObject), Dependency.Group("parameter", @params))
        {
            if (@params == null)
                @params = new List<IHasValue>();
            //IHasValue[] newParams = @params.ToArray();

            var preparedParams = new List<IHasValue>();
            mBaseObject = baseObject;
            mMethod = method;
            mShortName = shortName;

            ParameterInfo[] paramInfo = null;
            if (method is PropertyInfo)
            {
                mResultSystemType = ((PropertyInfo)method).PropertyType;
                paramInfo = ((PropertyInfo)method).GetIndexParameters();
            }
            else if (method is MethodInfo)
            {
                var mi = (MethodInfo)method;
                mResultSystemType = mi.ReturnType;
                paramInfo = mi.GetParameters();
            }
            else if (method is FieldInfo)
            {
                mResultSystemType = ((FieldInfo)method).FieldType;
                paramInfo = new ParameterInfo[] { };
            }

            for (int i = 0; i < @params.Count; i++)
            {
                if (i < paramInfo.Length)
                {
                    var sourceType = @params[i].ValueType;
                    var targetType = paramInfo[i].ParameterType;
                    if (casts[i] == null)
                    {
                        preparedParams.Add(@params[i]);
                    }
                    else
                    {
                        if (casts[i].GetType().IsArray)
                        {
                            var c2 = typeof(ArrayBuilder<>).MakeGenericType(targetType.GetElementType());
                            preparedParams.Add((IHasValue)Activator.CreateInstance(c2, new object[] { @params.Skip(i).ToArray(), casts[i] }));
                            // we have consumed all parameters
                            break;
                        }
                        else
                        {
                            var c3 = typeof(DelegateExpr<,>).MakeGenericType(sourceType, targetType);
                            preparedParams.Add((IHasValue)Activator.CreateInstance(c3, @params[i], casts[i], @params[i].ShortName));
                        }
                    }
                }
            }

            mPreparedParams = preparedParams.ToArray();
            mParamValues = new object[mPreparedParams.Length];

            if (method is PropertyInfo)
            {
                mValueDelegate = GetProperty;
            }
            else if (method is MethodInfo)
            {
                mValueDelegate = MakeGetMethod(baseObject, (MethodInfo)method);
            }
            else if (method is FieldInfo)
            {
                MakeGetField(baseObject, method);
            }
        }

        private T GetProperty()
        {
            Recalc();
            object res = ((PropertyInfo)mMethod).GetValue(mBaseValueObject, mParamValues);
            return (T)res;
        }

        private T GetMethod()
        {
            Recalc();
            object res = ((MethodInfo)mMethod).Invoke(mBaseValueObject, mParamValues);
            return (T)res;
        }

        private void MakeGetField(IHasValue baseObject, MemberInfo method)
        {
            Expression expr = null;
            if (baseObject != null)
            {
                var expectedType = typeof(IHasValue<>).MakeGenericType(baseObject.ValueType);
                expr = Expression.MakeMemberAccess(Expression.Constant(baseObject), expectedType.GetProperty("Value"));
                expr = Expression.Field(expr, (FieldInfo)method);
            }
            else
            {
                expr = Expression.Field(null, (FieldInfo)method);
            }
            var lambda = Expression.Lambda<Func<T>>(expr);
            mValueDelegate = lambda.Compile();
        }

        public double aaaaaa()
        {
            return Math.Sin((double)mPreparedParams[0].ObjectValue);
            /*
              IL_0000:  ldarg.0
              IL_0001:  ldfld      class Eval4.Core.IHasValue[] class Eval4.Core.CallMethodExpr`1<!T>::mPreparedParams
              IL_0006:  ldc.i4.0
              IL_0007:  ldelem.ref
              IL_0008:  callvirt   instance object Eval4.Core.IHasValue::get_ObjectValue()
              IL_000d:  unbox.any  [mscorlib]System.Double
              IL_0012:  call       float64 [mscorlib]System.Math::Sin(float64)
              IL_0017:  ret
             */
        }

        private Func<T> MakeGetMethod(IHasValue baseObject, MethodInfo mi)
        {
            var paramTypes = (from p in mPreparedParams select p.ValueType).ToArray();
            DynamicMethod meth = new DynamicMethod(
                "DynamicGetMethod",
                typeof(T),
                new Type[] { this.GetType() },
                this.GetType(),  // associate with a type
                true);
            ILGenerator il = meth.GetILGenerator();
            FieldInfo fiPreparedParams = this.GetType().GetField("mPreparedParams", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo miGetObjectValue = typeof(IHasValue).GetProperty("ObjectValue").GetGetMethod();
            if (!mi.IsStatic)
            {
                FieldInfo fiBaseObject = this.GetType().GetField("mBaseObject", BindingFlags.Instance | BindingFlags.NonPublic);
                il.Emit(OpCodes.Ldarg_0);         // this
                il.Emit(OpCodes.Ldfld, fiBaseObject); // this.mParams
                il.Emit(OpCodes.Callvirt, miGetObjectValue);
            }
            var methodParams = ((MethodInfo)mMethod).GetParameters();

            for (int i = 0; i < mPreparedParams.Length; i++)
            {
                
            
                il.Emit(OpCodes.Ldarg_0);         // this
                il.Emit(OpCodes.Ldfld, fiPreparedParams); // this.mParams
                il.Emit(OpCodes.Ldc_I4, i);       // i
                il.Emit(OpCodes.Ldelem_Ref);      // this.mParams[i]
                il.Emit(OpCodes.Callvirt, miGetObjectValue);
                var preparedType = mPreparedParams[i].ValueType;
                if (preparedType == typeof(string))
                {
                    throw new NotImplementedException("check this");
                }
                if (preparedType.IsValueType && methodParams[i].ParameterType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, preparedType);
                }
            }
            il.Emit(OpCodes.Call, mi);
            il.Emit(OpCodes.Ret);

            Delegate dlg = meth.CreateDelegate(typeof(Func<T>), this);
            return (Func<T>)dlg;
        }

        private void Recalc()
        {
            for (int i = 0; i <= mPreparedParams.Length - 1; i++)
            {
                mParamValues[i] = mPreparedParams[i].ObjectValue;
            }
            mBaseValueObject = mBaseObject.ObjectValue;
        }

        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mValueDelegate(); }
        }

        public override string ShortName
        {
            get { return mShortName; }
        }
    }

    public class GetArrayEntryExpr<T> : Expr<T>
    {

        private IHasValue withEventsField_mArray;
        private IHasValue[] mParams;
        private int[] mValues;
        private Type mResultSystemType;

        public IHasValue mArray
        {
            get { return withEventsField_mArray; }
            set
            {
                if (withEventsField_mArray != null)
                {
                    //throw new NotImplementedException();
                    //withEventsField_mArray.ValueChanged -= mBaseVariable_ValueChanged;
                }
                withEventsField_mArray = value;
                if (withEventsField_mArray != null)
                {
                    //throw new NotImplementedException();
                    //withEventsField_mArray.ValueChanged += mBaseVariable_ValueChanged;
                }
            }

        }

        public GetArrayEntryExpr(IHasValue array, List<IHasValue> @params)
            : base(new Dependency("Array", array), Dependency.Group("index", @params))
        {
            IHasValue[] newParams = @params.ToArray();
            int[] newValues = new int[@params.Count];

            mArray = array;
            mParams = newParams;
            mValues = newValues;
            mResultSystemType = array.ValueType.GetElementType();
        }

        public override T Value
        {
            get
            {
                object res = null;
                Array arr = (Array)mArray.ObjectValue;

                for (int i = 0; i <= mValues.Length - 1; i++)
                {
                    mValues[i] = System.Convert.ToInt32(mParams[i].ObjectValue);
                }

                res = arr.GetValue(mValues);
                return (T)res;
            }
        }

        public System.Type SystemType
        {
            get { return typeof(T); }
        }

        private void mBaseVariable_ValueChanged(object sender, System.EventArgs e)
        {
            throw new NotImplementedException();
            //if (ValueChanged != null) ValueChanged(sender, e);
        }

        public override string ShortName
        {
            get { return "ArrayEntry[]"; }
        }
    }

    public class OperatorIfExpr<T> : Expr<T>
    {
        private Type mSystemType;
        private IHasValue ifExpr;
        private IHasValue thenExpr;
        private IHasValue elseExpr;

        public OperatorIfExpr(IHasValue ifExpr, IHasValue thenExpr, IHasValue elseExpr)
            : base(new Dependency("if", ifExpr), new Dependency("then", thenExpr), new Dependency("else", elseExpr))
        {
            this.ifExpr = ifExpr;
            this.thenExpr = thenExpr;
            this.elseExpr = elseExpr;
            mSystemType = thenExpr.ValueType;
        }

        public override T Value
        {
            get
            {
                object result;
                var test = System.Convert.ToBoolean(ifExpr.ObjectValue);
                result = test ? thenExpr.ObjectValue : elseExpr.ObjectValue;

                if (result != null && result.GetType() != mSystemType) result = System.Convert.ChangeType(result, mSystemType);
                return (T)result;
            }
        }

        public override string ShortName
        {
            get { return "OperatorIf"; }
        }

    }

    internal class DelegateExpr<P1, T> : Expr<T>
    {
        private IHasValue<P1> mP1;
        private Func<P1, T> mDelegate;
        private string mShortName;

        public DelegateExpr(IHasValue<P1> p1, Func<P1, T> dlg, string shortName)
            : base(new Dependency("p1", p1))
        {
            System.Diagnostics.Debug.Assert(dlg != null);
            mP1 = p1;
            mDelegate = dlg;
            mShortName = shortName;
        }


        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mDelegate(mP1.Value); }
        }

        //public event ValueChangedEventHandler ValueChanged;

        public override string ShortName
        {
            get { return mShortName; }
        }

    }

    internal class DelegateExpr<P1, P2, T> : Expr<T>
    {
        private IHasValue<P1> mP1;
        private IHasValue<P2> mP2;
        private Func<P1, P2, T> mDelegate;
        private string mShortName;

        public DelegateExpr(IHasValue<P1> p1, IHasValue<P2> p2, Func<P1, P2, T> dlg, string shortName)
            : base(new Dependency("p1", p1), new Dependency("p2", p2))
        {
            mP1 = p1;
            mP2 = p2;
            mDelegate = dlg;
            mShortName = shortName;
        }

        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mDelegate(mP1.Value, mP2.Value); }
        }

        public override string ShortName
        {
            get { return mShortName; }
        }
    }

    class GetVariableFromBag<T> : Expr<T>
    {
        private string mVariableName;
        private IEvaluator mEvaluator;
        private Variable<T> mVariable;

        public GetVariableFromBag(IEvaluator evaluator, string variableName)
            : base(null)
        {
            mEvaluator = evaluator;
            mVariableName = variableName;
            mVariable = mEvaluator.GetVariable<T>(mVariableName);
            AddDependency(new Dependency("Variable " + variableName, mVariable));
        }

        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mVariable.Value; }
        }

        public override string ShortName
        {
            get { return "GetVar " + mVariableName; }
        }
    }

    public class Variable<T> : Expr<T>, IVariable
    {
        private T mValue;
        private string mVariableName;

        public Variable(T variableValue, string variableName)
            : base(null)
        {
            mValue = variableValue;
            mVariableName = variableName;
        }

        public override T Value
        {
            //[System.Diagnostics.DebuggerStepThrough()]
            get { return mValue; }
        }

        public override string ShortName
        {
            get { return mVariableName; }
        }

        public void SetValue(object value)
        {
            mValue = (T)value;
            base.RaiseValueChanged();
        }
    }
}
