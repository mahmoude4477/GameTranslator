# Hyphenate [![Build Status](https://travis-ci.org/vilic/hyphenate.svg)](https://travis-ci.org/vilic/hyphenate)

A simple helper function that splits and then joins words back together.

## Install

```sh
yarn add hyphenate
```

## Usage

```ts
import hyphenate from 'hyphenate';

hyphenate('hello, world!'); // "hello-world"
hyphenate('JSONDataItems'); // "JSON-Data-Items"
hyphenate('JSONDataItems', {lowerCase: true}); // "json-data-items"
```
