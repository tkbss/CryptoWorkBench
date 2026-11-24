using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Statement
    {
        
        protected virtual void VerifyTextChange() { }
        public virtual string Text { get; set { VerifyTextChange(); field = value; } }
        public Statement()
        {
            Text = string.Empty;    
        }
    }
}
