import Foundation

enum PlainTextImportError: LocalizedError {
    case noContent

    var errorDescription: String? {
        "No nonblank lines were found. Enter one item per line."
    }
}

enum PlainTextImport {
    static func parse(_ text: String) throws -> [ImportCandidate] {
        let lines = text.replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n").components(separatedBy: "\n")
        let result = lines.filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .map { ImportCandidate(selected: true, snippet: Snippet(text: $0), context: "") }
        guard !result.isEmpty else { throw PlainTextImportError.noContent }
        return result
    }
}
