import { readFile, writeFile } from 'node:fs/promises'

const version = process.argv[2]

if (!version || !/^\d+\.\d+\.\d+$/.test(version)) {
  throw new Error('A stable semantic version is required, for example 0.1.42')
}

async function updatePackage(path, update) {
  const packageJson = JSON.parse(await readFile(path, 'utf8'))
  update(packageJson)
  await writeFile(path, `${JSON.stringify(packageJson, null, 2)}\n`)
}

await updatePackage('Ui/package.json', (packageJson) => {
  packageJson.version = version
})

await updatePackage('Identity/package.json', (packageJson) => {
  packageJson.version = version
  packageJson.devDependencies['@pebble/ui'] = version
})
