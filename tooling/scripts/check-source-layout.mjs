// Copyright (c) 2026 unicbm. All rights reserved.
// Licensed under the GNU Affero General Public License v3.0 only.
// See LICENSE in the project root for license information.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
const { components } = JSON.parse(readFileSync('components.json', 'utf8'));
const modules = git('config', '--file', '.gitmodules', '--get-regexp', '^submodule\..*\.path$');
if (modules !== 'submodule.third_party/demoparser.path third_party/demoparser') {
  throw new Error('Only the maintained parser may be a source submodule.');
}
const entries = git('ls-files', '--stage').split('\n');
const links = entries.filter(line => line.startsWith('160000 '));
if (links.length !== 1 || !links[0].endsWith('\tthird_party/demoparser')) {
  throw new Error('Expected exactly one parser gitlink.');
}
const ids = new Set();
for (const c of components) {
  if (ids.has(c.id)) throw new Error(`Duplicate component: ${c.id}`);
  ids.add(c.id);
  if (c.source === 'product') {
    if (c.repository !== 'unicbm/demotracer' || !existsSync(c.path) ||
        existsSync(resolve(c.path, '.git')) || existsSync(resolve(c.path, '.gitmodules')) || existsSync(resolve(c.path, '.deps'))) {
      throw new Error(`Product module must use the single working tree: ${c.path}`);
    }
  } else if (c.source !== 'submodule' || c.id !== 'parser' || c.path !== 'third_party/demoparser' || c.repository !== 'unicbm/demoparser') {
    throw new Error(`Unsupported source entry: ${c.id}`);
  }
}
if (ids.size !== 7 || !ids.has('parser')) throw new Error('Incomplete component source inventory.');
if (git('config', '--file', '.gitmodules', '--get', 'submodule.third_party/demoparser.url') !== 'https://github.com/unicbm/demoparser.git') {
  throw new Error('Unexpected parser repository.');
}
if (process.argv.includes('--require-checkout')) {
  const expected = links[0].split(' ')[1];
  if (!existsSync('third_party/demoparser/.git') || git('-C', 'third_party/demoparser', 'rev-parse', 'HEAD') !== expected) {
    throw new Error('Initialize the parser at its committed gitlink before building.');
  }
}
console.log('Source layout verified: six product modules and one pinned parser.');
