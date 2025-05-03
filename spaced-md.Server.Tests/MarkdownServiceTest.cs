using System;
using System.Collections.Generic;
using Xunit;
using SpacedMd.Server.Services;

namespace spaced_md.Server.Tests
{
    public class MarkdownServiceTest
    {
        private readonly MarkdownService _service;
        
        public MarkdownServiceTest()
        {
            _service = new MarkdownService();
        }
        
        [Fact]
        public void GetHeadings_WithValidMarkdown_ReturnsExpectedHeadings()
        {
            string markdown = @"# Heading 1
Some content here.
## Subheading 1.1
More conteet here.
# Heading 2
Even more content.";

            var headings = _service.GetHeadings(markdown);

            // Expect three headings: "# Heading 1", "## Subheading 1.1", and "# Heading 2".
            Assert.Equal(3, headings.Count);
            Assert.Equal("# Heading 1", headings[0].Heading);
            Assert.Equal(1, headings[0].Level);
            Assert.Equal(1, headings[0].LineNumber);

            Assert.Equal("## Subheading 1.1", headings[1].Heading);
            Assert.Equal(2, headings[1].Level);

            Assert.Equal("# Heading 2", headings[2].Heading);
            Assert.Equal(1, headings[2].Level);
        }

        [Fact]
        public void GetSection_WithValidHeading_ReturnsSection()
        {
            string markdown = @"# Heading 1
Line 1 content.
## Subheading 1.1
Line 2 content.
Line 3 content.
# Heading 2
Line 4 content.
Line 5 content.";

            string section = _service.GetSection(markdown, "# Heading 1");

            string expectedSection = @"# Heading 1
Line 1 content.
## Subheading 1.1
Line 2 content.
Line 3 content.";

            Assert.Equal(expectedSection, section);
        }

        [Fact]
        public void GetSection_WithNonexistentHeading_ReturnsEmptyString()
        {
            string markdown = @"# Heading 1
Content.";
            
            string section = _service.GetSection(markdown, "Nonexistent");
            
            Assert.Equal(string.Empty, section);
        }

        [Fact]
        public void GetSection_WithNestedHeadings_ReturnsCorrectSection()
        {
            string markdown = @"# Chapter 1
Introduction content.
## Section 1.1
Content of section 1.1.
### Subsection 1.1.1
Details of subsection.
## Section 1.2
Content of section 1.2.
# Chapter 2
Using different chapter.";

            // Get section for "## Section 1.1"
            string section = _service.GetSection(markdown, "## Section 1.1");

            string expectedSection = @"## Section 1.1
Content of section 1.1.
### Subsection 1.1.1
Details of subsection.";

            Assert.Equal(expectedSection, section);
        }

        [Fact]
        public void GetSection_WithAdjacentHeadings_ReturnsCorrectSection()
        {
            string markdown = @"# Part A
Content for Part A.
# Part B
Content for Part B.
# Part C
Content for Part C.";

            // Get section for "# Part B"
            string section = _service.GetSection(markdown, "# Part B");

            string expectedSection = @"# Part B
Content for Part B.";

            Assert.Equal(expectedSection, section);
        }

        [Fact]
        public void GetSection_WithDeeplyNestedHeadings_ReturnsCorrectSection()
        {
            string markdown = @"# Root Heading
Root content.
## Level 1
Level 1 content.
### Level 2
Level 2 content.
#### Level 3
Level 3 content.
# Another Root Heading
Other root content.";

            // Get section for "### Level 2"
            string section = _service.GetSection(markdown, "### Level 2");

            string expectedSection = @"### Level 2
Level 2 content.
#### Level 3
Level 3 content.";

            Assert.Equal(expectedSection, section);
        }
    }
}
