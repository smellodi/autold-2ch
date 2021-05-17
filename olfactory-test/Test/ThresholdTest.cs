using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Olfactory.Test
{
    internal class ThresholdTest : ITest
    {
        public event EventHandler Continue = delegate { };

        public ThresholdTest()
        {
            _instructionsPage.Next += (s, e) => Continue(this, new EventArgs());
            _familiarizePage.Next += (s, e) => Continue(this, new EventArgs());
        }

        public Page NextPage()
        {
            _current = _current switch
            {
                null => _instructionsPage,
                Pages.ThresholdTest.Instructions _ => _familiarizePage,
                Pages.ThresholdTest.Familiarize _ => null,
                _ => null
            };

            return _current;
        }


        // Internal

        Pages.ThresholdTest.Instructions _instructionsPage = new Pages.ThresholdTest.Instructions();
        Pages.ThresholdTest.Familiarize _familiarizePage = new Pages.ThresholdTest.Familiarize();

        Page _current = null;
    }
}
