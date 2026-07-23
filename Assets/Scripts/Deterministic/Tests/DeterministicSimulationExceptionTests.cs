using System;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Deterministic.Tests
{
    [TestFixture]
    public class DeterministicSimulationExceptionTests
    {
        [Test]
        public void Construction_PreservesMessage()
        {
            var ex = new DeterministicSimulationException("spawn overflow");
            Assert.AreEqual("spawn overflow", ex.Message);
        }

        [Test]
        public void IsExceptionSubclass()
        {
            var ex = new DeterministicSimulationException("msg");
            Assert.IsInstanceOf<Exception>(ex);
        }

        [Test]
        public void Construction_WithInnerException_PreservesBoth()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new DeterministicSimulationException("outer", inner);
            Assert.AreEqual("outer", ex.Message);
            Assert.AreSame(inner, ex.InnerException);
        }
    }
}