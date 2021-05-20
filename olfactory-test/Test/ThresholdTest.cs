using System;
using System.Windows.Controls;

namespace Olfactory.Tests
{
    /// <summary>
    /// Manages the order of pages in the Threshold Test 
    /// </summary>
    public partial class ThresholdTest : ITest
    {
        public event EventHandler PageDone = delegate { };

        public ThresholdTest()
        {
            _instructionsPage.Next += (s, e) => PageDone(this, new EventArgs());
            _familiarizePage.Next += (s, e) => PageDone(this, new EventArgs());
            _resultPage.Next += (s, e) => PageDone(this, new EventArgs());
            _threePensPage.Next += (s, e) =>
            {
                _threePensPage.Init();
            };
            _threePensPage.Finished += (s, e) =>
            {
                // TODO: e is the resulting level, we should log it here
                _resultPage.SetPPM(e);
                PageDone(this, new EventArgs());
            };
        }

        public Page NextPage()
        {
            _current = _current switch
            {
                null => _instructionsPage,
                Pages.ThresholdTest.Instructions _ => _familiarizePage,
                Pages.ThresholdTest.Familiarize _ => _threePensPage,
                Pages.ThresholdTest.ThreePens _ => _resultPage,
                Pages.ThresholdTest.Result _ => null,
                _ => throw new NotImplementedException("Unhandled page in the Threhold Test"),
            };

            if (_current is Pages.ThresholdTest.ThreePens page)
            {
                page.Init();
            }

            return _current;
        }

        public Page Start()
        {
            _current = null;
            return NextPage();
        }


        // Internal

        Pages.ThresholdTest.Instructions _instructionsPage = new Pages.ThresholdTest.Instructions();
        Pages.ThresholdTest.Familiarize _familiarizePage = new Pages.ThresholdTest.Familiarize();
        Pages.ThresholdTest.ThreePens _threePensPage = new Pages.ThresholdTest.ThreePens();
        Pages.ThresholdTest.Result _resultPage = new Pages.ThresholdTest.Result();

        Page _current = null;
    }
}
