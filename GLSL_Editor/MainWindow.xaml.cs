using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GLSL_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void TextBox_keyUp(object sender, KeyEventArgs e)
        {
            //types
            //pre-processor
            //(in\s)|(out\s)|(#.*)|(\/.*)
            TextRange range = new TextRange(glslTextBox.Document.ContentStart, glslTextBox.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.White));

            Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex miscRegex = new Regex(@"(in\s|out\s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex commentRegex = new Regex(@"(\/\/.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var start = glslTextBox.Document.ContentStart;
            while (start != null && start.CompareTo(glslTextBox.Document.ContentEnd) < 0)
            {
                if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var match = typesRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match2 = miscRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match3 = commentRegex.Match(start.GetTextInRun(LogicalDirection.Forward));

                    if (match.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match.Index + match.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(50, 180, 250));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                        //start = textrange.End;
                    }
                    else if (match2.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match2.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match2.Index + match2.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(240, 100, 90));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                        //start = textrange.End;
                    }
                    else if (match3.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match3.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match3.Index + match3.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(100, 255, 100));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                        //start = textrange.End;
                    }

                }
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
        }
    }
}
