const headline = document.getElementById("Input_Headline");
const toggleHighlight = document.getElementById("toggle-headline-highlight");
const highlightStatus = document.getElementById("highlight-status");

toggleHighlight.hidden = false;
toggleHighlight.addEventListener("click", () => {
    const { value, selectionStart: start, selectionEnd: end } = headline;
    const selected = value.slice(start, end);
    if (!selected.trim() || selected.includes("*")) {
        highlightStatus.textContent = "Select the words you want to highlight or unhighlight, without the asterisks.";
        headline.focus();
        return;
    }

    const insideHighlight = value.slice(0, start).split("*").length % 2 === 0;
    if (insideHighlight && value[start - 1] === "*" && value[end] === "*") {
        headline.setRangeText(selected, start - 1, end + 1, "select");
        highlightStatus.textContent = "Highlight removed.";
    } else if (insideHighlight) {
        highlightStatus.textContent = "Select all the words between the asterisks to remove the highlight.";
    } else if (value.length + 2 > headline.maxLength) {
        highlightStatus.textContent = "Shorten the headline to make room for the two asterisks.";
    } else {
        headline.setRangeText("*" + selected + "*", start, end, "select");
        headline.setSelectionRange(start + 1, end + 1);
        highlightStatus.textContent = "Highlight added.";
    }

    headline.focus();
});
