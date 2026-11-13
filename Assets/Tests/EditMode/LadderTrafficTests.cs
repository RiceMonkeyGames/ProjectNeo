using NUnit.Framework;
using ProjectNeo.Siege;

namespace ProjectNeo.Tests
{
    public class LadderTrafficTests
    {
        [Test]
        public void ArrivalOrder_IsShared_AndRepeatedRequestsDoNotReorder()
        {
            var traffic = new LadderTraffic<object>();
            var first = new object(); var second = new object(); var third = new object();
            Assert.IsTrue(traffic.TryEnter(first));
            Assert.IsFalse(traffic.TryEnter(second));
            Assert.IsFalse(traffic.TryEnter(third));
            traffic.Join(second);
            traffic.Leave(first);
            Assert.IsFalse(traffic.TryEnter(third));
            Assert.IsTrue(traffic.TryEnter(second));
            traffic.Leave(second);
            Assert.IsTrue(traffic.TryEnter(third));
        }
        [Test]
        public void CancelledHeadAndOccupant_DoNotBlockNext_AndClearResetsAll()
        {
            var traffic = new LadderTraffic<object>();
            var first = new object(); var cancelled = new object(); var next = new object();
            traffic.TryEnter(first); traffic.Join(cancelled); traffic.Join(next);
            traffic.Leave(cancelled); traffic.Leave(first);
            Assert.IsTrue(traffic.TryEnter(next));
            traffic.Join(first); traffic.Clear();
            Assert.IsNull(traffic.Occupant); Assert.IsEmpty(traffic.Waiting);
            Assert.IsFalse(traffic.TryEnter(null));
            Assert.IsTrue(traffic.TryEnter(first));
        }
    }
}
