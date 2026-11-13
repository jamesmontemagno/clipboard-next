import SwiftUI
import UniformTypeIdentifiers

struct EditorView: View {
    let model: AppModel
    @State private var selection: UUID?
    @State private var importing = false

    private var selectedIndex: Int? {
        model.queue.snippets.firstIndex { $0.id == selection }
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 16) {
                Image(systemName: "clipboard").font(.largeTitle).foregroundStyle(.tint)
                VStack(alignment: .leading) {
                    Text("Clipboard Next").font(.title2.bold())
                    Text("\(model.queue.status.rawValue) · \(model.queue.progress)")
                        .foregroundStyle(.secondary)
                }
                Spacer()
                QueueControls(queue: model.queue, selectedID: selection)
                if model.queue.status != .running, selection != nil {
                    Button("Start Here") { model.queue.start(at: selection) }
                }
            }
            .padding(20)
            Divider()
            HSplitView {
                VStack(spacing: 0) {
                    List(selection: $selection) {
                        ForEach(Array(model.queue.snippets.enumerated()), id: \.element.id) { offset, item in
                            HStack(alignment: .top, spacing: 10) {
                                Text("\(offset + 1)").monospacedDigit().foregroundStyle(.secondary)
                                    .frame(width: 26, alignment: .trailing)
                                VStack(alignment: .leading, spacing: 4) {
                                    if !item.title.isEmpty {
                                        Text(item.title).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                                    }
                                    HStack {
                                        Text(item.text.isEmpty ? "Empty snippet" : item.text)
                                            .font(.system(.body, design: .monospaced)).lineLimit(3)
                                        if model.queue.index == offset {
                                            Image(systemName: model.queue.ownsClipboard ? "clipboard.fill" : "arrow.right.circle")
                                                .foregroundStyle(.tint)
                                                .help(model.queue.ownsClipboard ? "On clipboard" : "Current queue position")
                                        }
                                    }
                                }
                                Spacer(minLength: 0)
                                Button { delete(item.id) } label: {
                                    Image(systemName: "trash").foregroundStyle(.secondary)
                                }
                                .buttonStyle(.borderless)
                                .help("Delete snippet \(offset + 1)")
                                .accessibilityLabel("Delete snippet \(offset + 1)")
                                .disabled(model.queue.status == .running)
                            }
                            .padding(.vertical, 5)
                            .tag(item.id)
                        }
                    }
                    .overlay {
                        if model.queue.snippets.isEmpty {
                            ContentUnavailableView("Build your demo queue", systemImage: "text.badge.plus",
                                                   description: Text("Add snippets or import Markdown or plain text."))
                        }
                    }
                    HStack {
                        Button("Add", systemImage: "plus") {
                            let snippet = Snippet(text: "")
                            model.queue.update(model.queue.snippets + [snippet])
                            selection = snippet.id
                        }
                        Button("Clear All", systemImage: "trash") { confirmingClear = true }
                            .disabled(model.queue.snippets.isEmpty)
                        Spacer()
                        Button { move(-1) } label: { Image(systemName: "arrow.up") }
                            .disabled(selectedIndex == nil || selectedIndex == 0)
                            .help("Move up")
                        Button { move(1) } label: { Image(systemName: "arrow.down") }
                            .disabled(selectedIndex == nil || selectedIndex == model.queue.snippets.count - 1)
                            .help("Move down")
                    }
                    .padding(12)
                    .disabled(model.queue.status == .running)
                }
                .frame(minWidth: 320)

                VStack(alignment: .leading, spacing: 14) {
                    if let index = selectedIndex {
                        Text("SNIPPET \(index + 1)").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                        TextField("Title (optional)", text: binding(index, \.title))
                            .textFieldStyle(.roundedBorder)
                            .font(.caption)
                        TextEditor(text: binding(index, \.text))
                            .font(.system(.body, design: .monospaced))
                            .scrollContentBackground(.hidden)
                            .padding(10)
                            .background(.quaternary.opacity(0.4), in: RoundedRectangle(cornerRadius: 10))
                        Text("Text is copied exactly, including newlines. The app never runs commands or presses Return.")
                            .font(.caption).foregroundStyle(.secondary)
                    } else {
                        ContentUnavailableView("Select a snippet", systemImage: "text.cursor",
                                               description: Text("Edit commands and prompts here."))
                    }
                }
                .padding(20)
                .frame(minWidth: 340, maxHeight: .infinity)
                .disabled(model.queue.status == .running)
            }
            Divider()
            VStack(alignment: .leading, spacing: 8) {
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: 12) {
                        importButton
                        badgeToggle
                        delayControl
                    }
                    .fixedSize(horizontal: true, vertical: false)
                    VStack(alignment: .leading, spacing: 8) {
                        importButton
                        badgeToggle
                        delayControl
                    }
                }
                HStack {
                    if model.monitor.monitoring {
                        Label("Paste detection on", systemImage: "checkmark.circle").foregroundStyle(.green)
                    } else {
                        Button("Enable / Retry Paste Detection") { model.monitor.requestPermission() }
                        Button("Privacy Settings") { model.monitor.openSettings() }
                    }
                }
                if !model.monitor.monitoring {
                    HStack {
                        Text("Running: \(Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String ?? "Clipboard Next")")
                            .font(.caption).foregroundStyle(.secondary)
                        Button("Reveal This App") {
                            NSWorkspace.shared.activateFileViewerSelecting([Bundle.main.bundleURL])
                        }
                        .font(.caption)
                    }
                }
                if let error = model.monitor.error {
                    Text(error).font(.caption).foregroundStyle(.orange).textSelection(.enabled)
                } else if !model.monitor.monitoring {
                    Text("Manual mode. Enable Input Monitoring for automatic Command-V advancement.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                if let message = model.queue.message {
                    Text(message).font(.caption).foregroundStyle(.secondary)
                }
                if let error = model.queue.storageError {
                    HStack {
                        Text(error).font(.caption).foregroundStyle(.red).textSelection(.enabled)
                        Button("Save Current Queue…") { confirmingSave = true }
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(16)
        }
        .sheet(isPresented: $importing) { ImportView(queue: model.queue) }
        .confirmationDialog("Save the current queue, replacing any existing saved file?", isPresented: $confirmingSave) {
            Button("Replace Saved Queue", role: .destructive) { model.queue.retrySave() }
        }
        .confirmationDialog("Clear all \(model.queue.snippets.count) snippets?", isPresented: $confirmingClear) {
            Button("Clear All", role: .destructive) {
                model.queue.update([])
                selection = nil
            }
        } message: {
            Text("This removes every snippet from the queue. It can't be undone.")
        }
    }

    @State private var confirmingSave = false
    @State private var confirmingClear = false

    private var importButton: some View {
        Button("Import…", systemImage: "square.and.arrow.down") { importing = true }
            .disabled(model.queue.status == .running)
    }

    private var badgeToggle: some View {
        @Bindable var badge = model.badge
        return Toggle("Floating badge", isOn: $badge.isVisible).toggleStyle(.switch)
    }

    private var delayControl: some View {
        HStack(spacing: 6) {
            Text("Next after").fixedSize()
            Slider(
                value: Binding(
                    get: { Double(model.queue.advanceDelayMilliseconds) },
                    set: { model.queue.setAdvanceDelay(milliseconds: Int($0)) }),
                in: Double(DemoQueue.delayRange.lowerBound)...Double(DemoQueue.delayRange.upperBound),
                step: 50)
                .frame(width: 140)
                .accessibilityLabel("Next after")
            Text(String(format: "%.2f s", Double(model.queue.advanceDelayMilliseconds) / 1000))
                .monospacedDigit().foregroundStyle(.secondary)
                .fixedSize()
        }
        .help("How long to wait after Command-V before the next snippet replaces the clipboard. Increase it if a slow app pastes the wrong snippet.")
    }

    private func delete(_ id: UUID) {
        model.queue.update(model.queue.snippets.filter { $0.id != id })
        if selection == id { selection = nil }
    }

    private func binding(_ index: Int, _ keyPath: WritableKeyPath<Snippet, String>) -> Binding<String> {
        let id = model.queue.snippets[index].id
        return Binding {
            model.queue.snippets.first { $0.id == id }?[keyPath: keyPath] ?? ""
        } set: { value in
            var items = model.queue.snippets
            guard let position = items.firstIndex(where: { $0.id == id }) else { return }
            items[position][keyPath: keyPath] = value
            model.queue.update(items)
        }
    }

    private func move(_ direction: Int) {
        guard let index = selectedIndex else { return }
        var items = model.queue.snippets
        items.swapAt(index, index + direction)
        model.queue.update(items)
    }
}

struct ImportView: View {
    let queue: DemoQueue
    @Environment(\.dismiss) private var dismiss
    @AppStorage("importFormat") private var format: ImportFormat = .markdown
    @State private var source = ""
    @State private var wholeBlocks = false
    @State private var candidates: [ImportCandidate] = []
    @State private var error: String?
    @State private var openingFile = false
    @State private var confirmingReplace = false

    private var chosen: [Snippet] { candidates.filter(\.selected).map(\.snippet) }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("Import snippets").font(.title2.bold())
                Spacer()
                Button("Open File…") { openingFile = true }
            }
            HStack {
                Text("Format")
                Picker("Import format", selection: $format) {
                    Text("Markdown").tag(ImportFormat.markdown)
                    Text("Plain Text").tag(ImportFormat.plainText)
                }
                .pickerStyle(.segmented)
                .labelsHidden()
                .frame(width: 240)
            }
            HSplitView {
                TextEditor(text: $source)
                    .font(.system(.body, design: .monospaced))
                    .frame(minWidth: 300)
                    .accessibilityLabel(format == .markdown ? "Markdown presenter notes" : "Plain text, one item per line")
                    .onChange(of: source) { _, _ in clearPreview() }
                List {
                    ForEach($candidates) { $candidate in
                        Toggle(isOn: $candidate.selected) {
                            VStack(alignment: .leading, spacing: 4) {
                                if !candidate.context.isEmpty {
                                    Text(candidate.context).font(.caption).foregroundStyle(.secondary)
                                }
                                Text(candidate.snippet.text).font(.system(.caption, design: .monospaced))
                                    .lineLimit(5)
                            }
                        }
                        .toggleStyle(.checkbox)
                        .padding(.vertical, 4)
                    }
                }
                .frame(minWidth: 340)
                .overlay {
                    if candidates.isEmpty {
                        ContentUnavailableView("Preview before importing", systemImage: "doc.text.magnifyingglass",
                                               description: Text(format == .markdown
                                                   ? "Code blocks are selected by default. Blockquoted prompts are optional."
                                                   : "Each nonblank line becomes an untitled snippet, selected by default."))
                    }
                }
            }
            if format == .markdown {
                Toggle("Keep each code block as one multiline snippet", isOn: $wholeBlocks)
                    .onChange(of: wholeBlocks) { _, _ in clearPreview() }
            }
            Text(format == .markdown
                ? "By default, each nonblank code line becomes a snippet. Splitting is not shell-aware: keep whole blocks for continuations or scripts. Review commands before pasting."
                : "Each nonblank line becomes an untitled snippet. Whitespace and repeated lines are kept. Review commands before pasting.")
                .font(.caption).foregroundStyle(.secondary)
            if let error { Text(error).foregroundStyle(.red).font(.caption) }
            HStack {
                Button("Cancel") { dismiss() }
                Button("Preview") { preview() }.disabled(source.isEmpty)
                Spacer()
                Text("\(chosen.count) selected").foregroundStyle(.secondary)
                Button("Replace Queue…") {
                    if queue.snippets.isEmpty { commit(replace: true) }
                    else { confirmingReplace = true }
                }.disabled(chosen.isEmpty)
                Button("Append to Queue") { commit(replace: false) }
                    .disabled(chosen.isEmpty).buttonStyle(.borderedProminent)
            }
        }
        .padding(22)
        .frame(width: 840, height: 580)
        .onChange(of: format) { _, _ in clearPreview() }
        .fileImporter(isPresented: $openingFile, allowedContentTypes: [.plainText, .text]) { result in
            do {
                let url = try result.get()
                let accessed = url.startAccessingSecurityScopedResource()
                defer { if accessed { url.stopAccessingSecurityScopedResource() } }
                source = try String(contentsOf: url, encoding: .utf8)
                clearPreview()
            } catch {
                self.error = "Could not open file: \(error.localizedDescription)"
            }
        }
        .confirmationDialog("Replace all existing snippets?", isPresented: $confirmingReplace) {
            Button("Replace Queue", role: .destructive) { commit(replace: true) }
        }
    }

    private func clearPreview() {
        candidates = []
        error = nil
    }

    private func preview() {
        do {
            candidates = try format == .markdown
                ? MarkdownImport.parse(source, wholeBlocks: wholeBlocks)
                : PlainTextImport.parse(source)
            error = nil
        } catch {
            candidates = []
            self.error = error.localizedDescription
        }
    }

    private func commit(replace: Bool) {
        queue.update(replace ? chosen : queue.snippets + chosen)
        dismiss()
    }
}
