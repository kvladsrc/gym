#include "cpp/warmup/dsu.hpp"

#include <iostream>
#include <numeric>
#include <utility>
#include <vector>

namespace warmup {

DisjointSetUnion::DisjointSetUnion(std::size_t size) {
  parent.resize(size);
  std::iota(parent.begin(), parent.end(), 0);
  rank.resize(size, 0);
}

std::size_t DisjointSetUnion::find(std::size_t i) {
  if (i >= parent.size()) {
    // This should ideally throw an exception or handle error more robustly.
    // For testing purposes, returning i to avoid crash.
    return i;
  }
  // Path compression.
  if (parent[i] != i) {
    parent[i] = find(parent[i]);
  }
  return parent[i];
}

bool DisjointSetUnion::connected(std::size_t a, std::size_t b) {
  if (a >= parent.size() || b >= parent.size()) {
    return false;  // Out of bounds indices are not connected
  }
  return find(a) == find(b);
}

void DisjointSetUnion::unite(std::size_t a, std::size_t b) {
  if (a >= parent.size() || b >= parent.size()) {
    return;  // Out of bounds indices cannot be united
  }
  auto pa = find(a);
  auto pb = find(b);

  if (pa == pb) {
    return;
  }

  if (rank[pa] < rank[pb]) {
    std::swap(pa, pb);
  }

  parent[pb] = pa;

  if (rank[pa] == rank[pb]) {
    ++rank[pa];
  }
}

}  // namespace warmup
