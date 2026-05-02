window.logAnalyzerDownloads = {
    downloadText(fileName, contentType, content) {
        const blob = new Blob([content], { type: contentType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    }
};

window.logAnalyzerReports = {
    async downloadPdf(url, fileName, markdown) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                fileName,
                markdown
            })
        });

        if (!response.ok) {
            const message = await response.text();
            throw new Error(message || `PDF generation failed with status ${response.status}`);
        }

        const blob = await response.blob();
        const downloadUrl = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = downloadUrl;
        link.download = fileName || "incident-report.pdf";
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(downloadUrl);
    }
};

window.logAnalyzerMarkdownEditor = {
    initialize(element, dotNetReference, initialValue) {
        if (!element || typeof EasyMDE === "undefined") {
            return false;
        }

        if (element.__easyMde) {
            element.__easyMde.toTextArea();
            element.__easyMde = null;
        }

        const editor = new EasyMDE({
            element,
            initialValue: initialValue || element.value || "",
            autofocus: false,
            spellChecker: false,
            status: false,
            minHeight: "320px",
            autoDownloadFontAwesome: true,
            renderingConfig: {
                singleLineBreaks: false
            },
            toolbar: [
                "bold",
                "italic",
                "heading",
                "|",
                "quote",
                "code",
                "unordered-list",
                "ordered-list",
                "clean-block",
                "|",
                "link",
                "table",
                "horizontal-rule",
                "|",
                "undo",
                "redo",
                "|",
                "guide"
            ]
        });

        editor.codemirror.on("change", () => {
            dotNetReference.invokeMethodAsync("UpdateMarkdownFromEditor", editor.value());
        });

        const fitEditor = () => {
            editor.codemirror.setSize("100%", "100%");
            editor.codemirror.refresh();
        };

        fitEditor();
        window.setTimeout(fitEditor, 0);

        element.__easyMde = editor;
        return true;
    },

    setValue(element, value) {
        if (element?.__easyMde) {
            element.__easyMde.value(value || "");
            return;
        }

        if (element) {
            element.value = value || "";
        }
    },

    dispose(element) {
        if (element?.__easyMde) {
            element.__easyMde.toTextArea();
            element.__easyMde = null;
        }
    }
};
