(() => {
	const fields = document.querySelectorAll("input[data-phone-format]");
	if (fields.length === 0) {
		return;
	}

	function digitCount(value) {
		return (value.match(/[0-9]/g) || []).length;
	}

	function caretAfterDigits(value, count) {
		if (count === 0) {
			return value.startsWith("+") ? 1 : 0;
		}

		let digits = 0;
		for (let index = 0; index < value.length; index += 1) {
			if (/[0-9]/.test(value[index])) {
				digits += 1;
				if (digits === count) {
					return index + 1;
				}
			}
		}

		return value.length;
	}

	fields.forEach((field) => {
		const wrapper = field.closest(".phone-input");
		const region = wrapper && wrapper.querySelector("select");
		const status = wrapper && wrapper.parentElement.querySelector(".phone-format-status");
		if (!region) {
			return;
		}

		let timer;
		let generation = 0;
		let activeRequest;

		async function format() {
			const currentGeneration = ++generation;
			const originalValue = field.value;
			const originalCaret = field.selectionStart ?? originalValue.length;
			const digitsBeforeCaret = digitCount(originalValue.slice(0, originalCaret));
			if (activeRequest) {
				activeRequest.abort();
			}
			activeRequest = new AbortController();

			try {
				const response = await fetch("/phone-format", {
					method: "POST",
					headers: { "Content-Type": "application/json" },
					body: JSON.stringify({ value: originalValue, regionCode: region.value }),
					signal: activeRequest.signal
				});
				if (!response.ok) {
					throw new Error(`Phone formatting request failed with status ${response.status}.`);
				}

				const result = await response.json();
				if (currentGeneration !== generation || field.value !== originalValue) {
					return;
				}

				field.value = result.formatted;
				const caret = caretAfterDigits(result.formatted, digitsBeforeCaret);
				field.setSelectionRange(caret, caret);
				if (status) {
					status.textContent = "";
				}
			} catch (error) {
				if (error.name === "AbortError") {
					return;
				}

				console.error("Automatic phone formatting failed.", error);
				if (status) {
					status.textContent = "Automatic formatting is unavailable; enter the complete number manually.";
				}
			}
		}

		function scheduleFormat(delay) {
			generation += 1;
			clearTimeout(timer);
			if (activeRequest) {
				activeRequest.abort();
			}
			timer = setTimeout(format, delay);
		}

		field.addEventListener("input", () => scheduleFormat(75));
		region.addEventListener("change", () => scheduleFormat(0));
		if (field.value) {
			scheduleFormat(0);
		}
	});
})();
