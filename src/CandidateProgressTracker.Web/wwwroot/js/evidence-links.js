(() => {
    document.querySelectorAll('[data-evidence-editor]').forEach(editor => {
        const rows = editor.querySelector('[data-link-rows]');
        const add = editor.querySelector('[data-add-link]');
        const status = editor.querySelector('[data-link-status]');
        const template = rows.firstElementChild.cloneNode(true);
        const update = () => {
            [...rows.children].forEach((row, index) => {
                row.querySelector('legend').textContent = `Link ${index + 1}`;
                row.querySelector('[data-remove-link]').setAttribute('aria-label', `Remove link ${index + 1}`);
                row.querySelectorAll('[data-link-field]').forEach(input => {
                    const field = input.dataset.linkField;
                    input.name = `Input.Evidence[${index}].${field}`;
                    input.id = `evidence-${index}-${field}`;
                    input.previousElementSibling.htmlFor = input.id;
                    const message = input.nextElementSibling;
                    message.setAttribute('data-valmsg-for', input.name);
                });
            });
            add.disabled = rows.children.length >= 10;
        };
        const blankRow = () => {
            const row = template.cloneNode(true);
            row.querySelectorAll('input').forEach(input => { input.value = ''; input.classList.remove('input-validation-error'); });
            row.querySelectorAll('[data-valmsg-for]').forEach(message => {
                message.textContent = '';
                message.classList.remove('field-validation-error');
                message.classList.add('field-validation-valid');
            });
            return row;
        };
        add.addEventListener('click', () => {
            if (rows.children.length >= 10) return;
            const row = blankRow();
            rows.append(row);
            update();
            row.querySelector('input').focus();
            status.textContent = `${rows.children.length} link rows. Maximum 10.`;
        });
        rows.addEventListener('click', event => {
            const remove = event.target.closest('[data-remove-link]');
            if (!remove) return;
            const row = remove.closest('[data-link-row]');
            const next = row.nextElementSibling || row.previousElementSibling;
            row.remove();
            if (!rows.children.length) rows.append(blankRow());
            update();
            (next || rows.firstElementChild).querySelector('input').focus();
            status.textContent = `Link removed. ${rows.children.length} link rows.`;
        });
        update();
    });
})();
