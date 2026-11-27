(() => {
	const container = document.getElementById("test-results");
	const template = document.getElementById("test-result-template");
	const addButton = document.getElementById("add-test-result");
	const countLabel = document.getElementById("test-result-count");
	if (!container || !template || !addButton || !countLabel) {
		return;
	}

	const maximum = Number.parseInt(container.dataset.maxTests || "50", 10);

	function updateRows() {
		const rows = Array.from(container.querySelectorAll(".test-result-row"));
		rows.forEach((row, index) => {
			row.querySelector(".test-row-number").textContent = String(index + 1);
			row.querySelectorAll("[name]").forEach((element) => {
				element.name = element.name.replace(/CheckupInput\.Tests\[\d+\]/, `CheckupInput.Tests[${index}]`);
			});
			row.querySelectorAll("[id]").forEach((element) => {
				element.id = element.id.replace(/-\d+$/, `-${index}`);
			});
			row.querySelectorAll("label[for]").forEach((element) => {
				element.htmlFor = element.htmlFor.replace(/-\d+$/, `-${index}`);
			});
		});

		countLabel.textContent = `${rows.length} of ${maximum} tests added.`;
		addButton.disabled = rows.length >= maximum;
	}

	function addRow(test) {
		const index = container.querySelectorAll(".test-result-row").length;
		if (index >= maximum) {
			return;
		}

		const html = template.innerHTML
			.replace(/__index__/g, String(index))
			.replace(/__number__/g, String(index + 1));
		container.insertAdjacentHTML("beforeend", html);
		const row = container.lastElementChild;
		const values = {
			Id: test.Id || 0,
			TestDate: test.TestDate ? String(test.TestDate).substring(0, 10) : new Date().toISOString().substring(0, 10),
			TestName: test.TestName || "",
			Result: test.Result || "",
			Unit: test.Unit || "",
			ReferenceRange: test.ReferenceRange || "",
			Notes: test.Notes || ""
		};

		Object.keys(values).forEach((key) => {
			const input = row.querySelector(`[name$=".${key}"]`);
			if (input) {
				input.value = values[key];
			}
		});

		updateRows();
	}

	container.addEventListener("click", (event) => {
		const removeButton = event.target.closest("[data-remove-test]");
		if (!removeButton) {
			return;
		}

		removeButton.closest(".test-result-row").remove();
		updateRows();
	});

	addButton.addEventListener("click", () => addRow({}));

	const initialTests = JSON.parse(container.dataset.initialTests || "[]");
	initialTests.forEach(addRow);
	updateRows();
})();
