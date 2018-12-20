using BruTile;
using NUnit.Framework;
using Terrain.ExtensionMethods;

namespace Terrain.Tests
{
    public class TileIndexTests
    {

        [Test]
        public void TileIndexToStringWorks()
        {
            // arrange
            var t = new TileIndex(100,200,"0");

            // act
            var index = t.ToIndexString();

            // assert
            Assert.IsTrue(index == "0/100/200");
        }
    }
}

