using System;
using CmisSync.Lib;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace TestLibrary
{
    [TestFixture]
    public class EncodingTest
    {
        [Test]
        public void IsoEncodingTest()
        {
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("abcdefghijklmnopqrstuvwxyz"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("1234567890"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("ÄÖÜäöüß"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("-_.:,;#+*?!"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("/\\|¦<>§$%&()[]{}"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("'\"´`"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("@~¹²³±×"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("¡¢£¤¥¨©ª«¬®¯°µ¶·¸º»¼¼¾¿"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝ"));
            Assert.IsTrue(CmisSync.Lib.Utils.IsValidISO88591("Þàáâãäåæçèéê"));
            Assert.IsFalse(CmisSync.Lib.Utils.IsValidISO88591("–€"));
        }
    }
}

