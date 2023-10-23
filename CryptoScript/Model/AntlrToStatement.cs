using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    
    public class AntlrToStatement : CryptoScriptBaseVisitor<Statement>
    {
        
       
        private List<VariableDeclaration> _variables=new List<VariableDeclaration>();
        public List<string> SemanticErrors { get; set; }

        public AntlrToStatement()
        {
                       
        }

        public override Statement VisitArgument([NotNull] CryptoScriptParser.ArgumentContext context)
        {
            var res1=context.functionCall();
            
            var res3=context.expression();
            if (res1 != null)
            {
               var fc=VisitFunctionCall(res1) as FunctionCall;
                if (fc != null)
                {
                    var expr=Expression.Create(fc.ReturnValue);
                    ArgumentExpression argExpr = new ArgumentExpression() { Expr = expr };
                    return argExpr;
                }
               
            }
            var res2 = context.ID();
            if (res2 != null)
            {
                string Id= res2.GetText();
                //Id is a variable so
                if (!_variables.Any(d => d.Id == Id))
                {
                    SemanticErrors.Add("Error  variable : " + Id + " is not declared");
                    throw new Exception("semantic error");
                }
                ArgumentVariable argVar = new ArgumentVariable();
                argVar.Id = _variables.First(v=>v.Id==Id);
                return argVar;
            }
            if(res3 != null) 
            {
                var expr=Expression.Create(res3.HEX_STRING().GetText());
                ArgumentExpression argExpr=new ArgumentExpression() { Expr=expr };
                return argExpr;
            }
            ArgumentVariable arg = new ArgumentVariable();
            return arg;
        }

        public override Statement VisitArguments([NotNull] CryptoScriptParser.ArgumentsContext context)
        {
            
            return base.VisitArguments(context);
        }

        public override Statement VisitDeclaration([NotNull] CryptoScriptParser.DeclarationContext context)
        {
            string Id = context.GetChild(1).GetText();
            if (_variables.Any(d => d.Id == Id))
            {
                SemanticErrors.Add("Error  variable : " + Id + " alredy exists");
                throw new Exception();
            }
            
            var newVariable = new VariableDeclaration();
            newVariable.Id = Id;    
            var typeCntx = context.type();
            newVariable.Type=(Type)VisitType(typeCntx);
            var fcontext=context.functionCall();
            var exprContext=context.expression();
            Statement stmt=null;
            if(fcontext!=null) { stmt=VisitFunctionCall(fcontext); }
            if (exprContext != null) { stmt=VisitExpression(exprContext); }
            if(stmt is FunctionCall functionCall) 
            {
                newVariable.Value = functionCall.ReturnValue;
                _variables.Add(newVariable);
            }
            if(stmt is Expression expressionHex) 
            {
                newVariable.Value = expressionHex.Value();
                _variables.Add(newVariable);
            }

            return newVariable;
        }

        public override Statement VisitExpression([NotNull] CryptoScriptParser.ExpressionContext context)
        {
            return Expression.Create(context.GetText());
            
        }

        public override Statement VisitFunctionCall([NotNull] CryptoScriptParser.FunctionCallContext context)
        {
            FunctionCall fc = new FunctionCall();
            string functionName = context.ID().GetText();
            fc.CallText = context.GetText();
            fc.Name = functionName;
            var arguments = context.arguments()?.argument();            
            if (arguments == null || arguments.Length == 0)
            {
                //return functions[functionName](new int[0]);
                fc.Call();
                return fc;
            }

            Statement[] argValues = (Statement[])arguments.Select(arg => Visit(arg)).ToArray();
            foreach(var a in argValues) 
            {
                var v = a as Argument;
                fc.Arguments.Add(v);
            }
            fc.Call();
            //var evaluation= functions[functionName](argValues);
            //return functions[functionName](argValues);
            return fc;
        }

        public override Statement VisitStatement([NotNull] CryptoScriptParser.StatementContext context)
        {
            var fcContext=context.functionCall();
            if (fcContext != null)
            {
                var fc = VisitFunctionCall(fcContext);
                return fc;
            }
            return base.VisitStatement(context);
        }

        public override Statement VisitType([NotNull] CryptoScriptParser.TypeContext context)
        {
            Type type =new Type();
            type.Check(context.GetText());
            type.Id=context.GetText();  
            return type;    
        }
    }
}
