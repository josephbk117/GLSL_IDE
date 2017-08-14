using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GLSL_Editor
{
    class TextEditorTypeAndScrollHelper
    {
        public enum ShaderType
        {
            VERTEX, FRAGMENT
        }
        private ShaderType shaderType;
        private RichTextBox shaderTextBox;
        private RichTextBox lineNumberDisplay;

        public TextEditorTypeAndScrollHelper(ShaderType shaderType, RichTextBox shaderTextBox, RichTextBox lineNumberTextBox)
        {
            this.shaderType = shaderType;
            this.shaderTextBox = shaderTextBox;
            this.lineNumberDisplay = lineNumberTextBox;
        }
        public TextEditorTypeAndScrollHelper()
        {
            this.shaderType = ShaderType.VERTEX;
        }

        public ShaderType GetShaderType()
        {
            return shaderType;
        }
        public RichTextBox GetShaderTextBox()
        {
            return shaderTextBox;
        }
        public RichTextBox GetCorrespondingLineTextBox()
        {
            return lineNumberDisplay;
        }
        public void SetShaderTextBox(RichTextBox rxt)
        {
            shaderTextBox = rxt;
        }
        public void SetCorrespondingLineNumberTextBox(RichTextBox rxt)
        {
            lineNumberDisplay = rxt;
        }
        public void SetShaderType(ShaderType shaderType)
        {
            this.shaderType = shaderType;
        }
    }
}
