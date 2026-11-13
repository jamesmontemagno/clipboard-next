import Foundation

enum ImportFormat: String {
    case markdown
    case plainText
}

struct ImportCandidate: Identifiable {
    let id = UUID()
    var selected: Bool
    let snippet: Snippet
    let context: String
}

enum MarkdownImportError: LocalizedError {
    case unclosedFence(Int)
    case noContent

    var errorDescription: String? {
        switch self {
        case .unclosedFence(let line): "Unclosed code fence at line \(line). Close it before importing."
        case .noContent: "No code blocks or blockquoted prompts were found."
        }
    }
}

enum MarkdownImport {
    static func parse(_ markdown: String, wholeBlocks: Bool) throws -> [ImportCandidate] {
        let lines = markdown.replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n").components(separatedBy: "\n")
        var result: [ImportCandidate] = []
        var heading = "Untitled section"
        var headingFound = false
        var i = 0
        while i < lines.count {
            let trimmed = lines[i].trimmingCharacters(in: .whitespaces)
            if let opening = fence(trimmed) {
                let startLine = i + 1
                let language = String(trimmed.dropFirst(opening.count)).trimmingCharacters(in: .whitespaces)
                i += 1
                var content: [String] = []
                var closed = false
                while i < lines.count {
                    let end = lines[i].trimmingCharacters(in: .whitespaces)
                    if let closing = fence(end), closing.character == opening.character,
                       closing.count >= opening.count,
                       end.dropFirst(closing.count).trimmingCharacters(in: .whitespaces).isEmpty {
                        closed = true
                        break
                    }
                    content.append(lines[i])
                    i += 1
                }
                guard closed else { throw MarkdownImportError.unclosedFence(startLine) }
                let texts = wholeBlocks ? [content.joined(separator: "\n")] : content
                for text in texts where !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    result.append(ImportCandidate(
                        selected: true,
                        snippet: Snippet(title: headingFound ? heading : "", text: text),
                        context: language.isEmpty ? heading : "\(heading) · \(language)"))
                }
            } else if trimmed.hasPrefix(">") {
                var content: [String] = []
                while i < lines.count {
                    let line = lines[i].trimmingCharacters(in: .whitespaces)
                    guard line.hasPrefix(">") else { break }
                    var text = String(line.dropFirst())
                    if text.hasPrefix(" ") { text.removeFirst() }
                    content.append(text)
                    i += 1
                }
                let text = content.joined(separator: "\n")
                if !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    result.append(ImportCandidate(
                        selected: false, snippet: Snippet(title: headingFound ? heading : "", text: text),
                        context: "\(heading) · Prompt"))
                }
                continue
            } else if trimmed.hasPrefix("#") {
                let title = trimmed.drop(while: { $0 == "#" })
                if title.first == " " {
                    heading = title.trimmingCharacters(in: .whitespaces)
                    headingFound = true
                }
            }
            i += 1
        }
        guard !result.isEmpty else { throw MarkdownImportError.noContent }
        return result
    }

    private static func fence(_ line: String) -> (character: Character, count: Int)? {
        guard let first = line.first, first == "`" || first == "~" else { return nil }
        let count = line.prefix(while: { $0 == first }).count
        return count >= 3 ? (first, count) : nil
    }
}
