
import { marked } from 'marked';

export function useMarkdownService() {
  // TODO: not sure if the logic is ok here or only use the one in the backedn.
  // the client already has the full file content locally, why get sections again from the server?
  async function getSectionContent(markdown: string, section: string): Promise<string> {
    const lines = markdown.split('\n');
    const startIndex = lines.findIndex(line => line.trim() === section);
    if (startIndex === -1) {
      return "";
    }
    const headingMatch = section.trim().match(/^(#+)/);
    const headingLevel = headingMatch ? headingMatch[1].length : 0;

    let content = lines[startIndex] + '\n';
    for (let i = startIndex + 1; i < lines.length; i++) {
      const line = lines[i];
      if (line.trim().startsWith('#')) {
        const currentMatch = line.trim().match(/^(#+)/);
        const currentLevel = currentMatch ? currentMatch[1].length : 0;
        if (currentLevel <= headingLevel) break;
      }
      content += line + '\n';
    }
    return content;
  }


  async function mdToHtml(markdown: string): Promise<string> {
    return marked.parse(markdown);
  }


  return { getSectionContent, mdToHtml };
}
