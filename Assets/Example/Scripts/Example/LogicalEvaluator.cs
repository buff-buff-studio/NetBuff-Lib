using NetBuff.Components;
using UnityEngine;

namespace ExamplePlatformer
{
    [RequireComponent(typeof(LogicOutput))]
    public class LogicalEvaluator : MonoBehaviour
    {
        public LogicInput[] inputs;
        
        public string expression = "(a && b) || c";
        private bool _lastOutput;
        private bool _hasOutput;

        private LogicOutput _output;
        public LogicOutput Output => _output ??= GetComponent<LogicOutput>();

        private void OnEnable()
        {
            InvokeRepeating(nameof(UpdateValue), 0, 0.1f);
        }
        
        private void OnDisable()
        {
            CancelInvoke(nameof(UpdateValue));
        }

        private void UpdateValue()
        {
            
            var n = EvaluateExpression();
            if(_hasOutput && n == _lastOutput)
                return;
            
            _lastOutput = n;
            _hasOutput = true;
            Output.OnOutputChanged(n);
        }

        private bool EvaluateExpression()
        {
            var exp = expression;
            
            for (var i = 0; i < inputs.Length; i++)
            {
                exp = exp.Replace((char) ('a' + i), inputs[i].GetInputValue() ? '1' : '0');
            }
            
            while (exp != "1" && exp != "0")
            {
                exp = exp.Replace(" ", "");
                exp = exp.Replace("1&&1", "1");
                exp = exp.Replace("1&&0", "0");
                exp = exp.Replace("0&&1", "0");
                exp = exp.Replace("0&&0", "0");
                
                exp = exp.Replace("1||1", "1");
                exp = exp.Replace("1||0", "1");
                exp = exp.Replace("0||1", "1");
                exp = exp.Replace("0||0", "0");
                
                exp = exp.Replace("1^1", "0");
                exp = exp.Replace("1^0", "1");
                exp = exp.Replace("0^1", "1");
                exp = exp.Replace("0^0", "0");
                
                exp = exp.Replace("!1", "0");
                exp = exp.Replace("!0", "1");
                
                exp = exp.Replace("(0)", "0");
                exp = exp.Replace("(1)", "1");
                
            }
            
            return exp == "1";
        }
    }

    public abstract class LogicInput : NetworkBehaviour
    {
        public abstract bool GetInputValue();
    }
    
    public abstract class LogicOutput : NetworkBehaviour
    {
        public abstract void OnOutputChanged(bool value);
    }
}