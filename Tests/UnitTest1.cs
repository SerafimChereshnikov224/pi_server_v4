using Xunit;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.interpreter.core.parser;

namespace PiServer.version_2.interpreter.tests
{
    public class PiParserTests
    {
        [Fact]
        public void ParseNullProcess()
        {
            var parser = new PiParser("0");
            var result = parser.Parse();

            Assert.IsType<NullProcess>(result);
            Assert.Equal("0", result.ToString());
        }

        [Fact]
        public void ParseOutputProcess()
        {
            var parser = new PiParser("x![y].0");
            var result = parser.Parse();

            var output = Assert.IsType<OutputProcess>(result);
            Assert.Equal("x", output.Channel);
            Assert.Equal("y", output.Message);
            Assert.IsType<NullProcess>(output.Continuation);
        }

        [Fact]
        public void ParseInputProcess()
        {
            var parser = new PiParser("a?(b).0");
            var result = parser.Parse();

            var input = Assert.IsType<InputProcess>(result);
            Assert.Equal("a", input.Channel);
            Assert.Equal("b", input.Variable);
            Assert.IsType<NullProcess>(input.Continuation);
        }

        [Fact]
        public void ParseParallelProcess()
        {
            var parser = new PiParser("x![y].0 | a?(b).0");
            var result = parser.Parse();

            var parallel = Assert.IsType<ParallelProcess>(result);
            var processes = parallel.Processes;

            Assert.Equal(2, processes.Count);
            Assert.IsType<OutputProcess>(processes[0]);
            Assert.IsType<InputProcess>(processes[1]);

            // Дополнительные проверки
            var output = (OutputProcess)processes[0];
            Assert.Equal("x", output.Channel);
            Assert.Equal("y", output.Message);

            var input = (InputProcess)processes[1];
            Assert.Equal("a", input.Channel);
            Assert.Equal("b", input.Variable);
        }

        [Fact]
        public void ParsesRestrictionWithBraces()
        {
            var input = "{*x}x![y].0";
            var parser = new PiParser(input);
            var result = parser.Parse();

            var restriction = Assert.IsType<RestrictionProcess>(result);
            Assert.Equal("x", restriction.Name);
            Assert.IsType<OutputProcess>(restriction.Body);
        }


        //[Fact]
        //public void ParseNestedProcesses()
        //{
        //    var parser = new PiParser("(?x)(x![z].0 | a?(b).b![x].0) | c?(d).0");
        //    var result = parser.Parse();

        //    var parallel = Assert.IsType<ParallelProcess>(result);
        //    var restriction = Assert.IsType<RestrictionProcess>(parallel.Left);
        //    var innerParallel = Assert.IsType<ParallelProcess>(restriction.Body);
        //}

        [Fact]
        public void ParseComplexExample()
        {
            var input = "({*x}(x![z].0 | x?(y).y![x].x?(y).0)) | z?(v).v![v].0";
            var parser = new PiParser(input);
            var result = parser.Parse();

            // Проверяем верхний уровень
            var parallel = Assert.IsType<ParallelProcess>(result);
            Assert.Equal(2, parallel.Processes.Count);

            // Проверяем левую часть (ограничение)
            var restriction = Assert.IsType<RestrictionProcess>(parallel.Processes[0]);
            Assert.Equal("x", restriction.Name);

            // Проверяем внутреннюю параллельную композицию
            var innerParallel = Assert.IsType<ParallelProcess>(restriction.Body);
            Assert.Equal(2, innerParallel.Processes.Count);

            // Проверяем правую часть (input)
            var inputProcess = Assert.IsType<InputProcess>(parallel.Processes[1]);
            InputProcess p1 = (InputProcess) parallel.Processes[1];
            Assert.IsType<OutputProcess>(p1.Continuation);
            Assert.Equal("z", inputProcess.Channel);
        }

        [Fact]
        public void ThrowsOnInvalidSyntax_MissingBracket()
        {
            var parser = new PiParser("x![y.0");
            Assert.Throws<Exception>(() => parser.Parse());
        }

        [Fact]
        public void ThrowsOnInvalidSyntax_UnknownSymbol()
        {
            var parser = new PiParser("x@[y].0");
            Assert.Throws<Exception>(() => parser.Parse());
        }

        [Fact]
        public void ThrowsOnEmptyInput()
        {
            Assert.Throws<ArgumentException>(() => new PiParser(""));
        }

        [Fact]
        public void ToString_ProducesCorrectOutput()
        {
            var process = new ParallelProcess(new List<Process>
                {
                    new OutputProcess("x", "y", new NullProcess()),
                    new InputProcess("a", "b", new NullProcess())
                });

            Assert.Equal("(x![y].0 | a?(b).0)", process.ToString());
        }
    }
}