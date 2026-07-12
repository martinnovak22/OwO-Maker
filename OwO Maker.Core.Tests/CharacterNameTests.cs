using OwO_Maker.Core;
using System.Text;
using Xunit;

namespace OwO_Maker.Core.Tests
{
    public class CharacterNameTests
    {
        private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

        [Fact]
        public void TryDecode_ValidName_ReturnsName()
        {
            Assert.True(CharacterName.TryDecode(7, Ascii("Panda~X"), out var name));
            Assert.Equal("Panda~X", name);
        }

        [Fact]
        public void TryDecode_LengthShorterThanBuffer_TakesOnlyPrefix()
        {
            // buffer may be read longer than the Delphi length prefix says
            Assert.True(CharacterName.TryDecode(5, Ascii("PandaGARBAGE"), out var name));
            Assert.Equal("Panda", name);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(33)] // above MaxLength
        [InlineData(100_000)] // garbage length prefix (wrong offset)
        public void TryDecode_ImplausibleLength_Fails(int length)
        {
            Assert.False(CharacterName.TryDecode(length, Ascii("Panda"), out _));
        }

        [Fact]
        public void TryDecode_BufferShorterThanLength_Fails()
        {
            Assert.False(CharacterName.TryDecode(10, Ascii("abc"), out _));
        }

        [Fact]
        public void TryDecode_NullBuffer_Fails()
        {
            Assert.False(CharacterName.TryDecode(5, null!, out _));
        }

        [Fact]
        public void TryDecodeUtf16_ValidName_ReturnsName()
        {
            Assert.True(CharacterName.TryDecodeUtf16(5, Encoding.Unicode.GetBytes("Panda"), out var name));
            Assert.Equal("Panda", name);
        }

        [Fact]
        public void TryDecodeUtf16_AsciiBytes_Fails()
        {
            // plain ASCII data misread as UTF-16 must not produce a name
            Assert.False(CharacterName.TryDecodeUtf16(5, Ascii("PandaPanda"), out _));
        }

        [Fact]
        public void TryDecodeUtf16_BufferShorterThanLength_Fails()
        {
            Assert.False(CharacterName.TryDecodeUtf16(10, Encoding.Unicode.GetBytes("abc"), out _));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(33)]
        public void TryDecodeUtf16_ImplausibleLength_Fails(int length)
        {
            Assert.False(CharacterName.TryDecodeUtf16(length, Encoding.Unicode.GetBytes("Panda"), out _));
        }

        [Fact]
        public void TryDecode_NonPrintableBytes_Fails()
        {
            // pointer read from a wrong offset typically yields binary junk
            Assert.False(CharacterName.TryDecode(4, new byte[] { 0x01, 0x00, 0xFF, 0x7F }, out _));
        }

        [Fact]
        public void TryDecode_SpaceInName_Fails()
        {
            // NosTale character names have no spaces
            Assert.False(CharacterName.TryDecode(5, Ascii("a b c"), out _));
        }

        [Theory]
        [InlineData("Panda", true)]
        [InlineData("xX.Panda.Xx", true)]
        [InlineData("abc123", true)]
        [InlineData("", false)]
        [InlineData("has space", false)]
        [InlineData("ThisNameIsWayTooLongToBeARealCharacterName", false)]
        public void IsPlausible_Cases(string name, bool expected)
        {
            Assert.Equal(expected, CharacterName.IsPlausible(name));
        }
    }
}
