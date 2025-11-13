#include "cpp/warmup/minimum_spanning_tree.hpp"

#include <algorithm>
#include <utility>
#include <vector>

namespace warmup {

class DSU {
 public:
  explicit DSU(int n) : parent_(n), rank_(n, 1) {
    for (int i = 0; i < n; ++i) {
      parent_[i] = i;
    }
  }

  int find(int i) {
    if (parent_[i] == i) {
      return i;
    }
    return parent_[i] = find(parent_[i]);
  }

  void unite(int i, int j) {
    int root_i = find(i);
    int root_j = find(j);
    if (root_i != root_j) {
      if (rank_[root_i] < rank_[root_j]) {
        std::swap(root_i, root_j);
      }
      parent_[root_j] = root_i;
      if (rank_[root_i] == rank_[root_j]) {
        rank_[root_i]++;
      }
    }
  }

 private:
  std::vector<int> parent_;
  std::vector<int> rank_;
};

bool compare_edges(const edge &a, const edge &b) { return a.first < b.first; }

std::vector<edge> kruskal(int v, std::vector<edge> &edges) {
  if (v == 0) {
    return {};
  }

  std::sort(edges.begin(), edges.end(), compare_edges);

  DSU dsu(v);
  std::vector<edge> mst;
  for (const auto &e : edges) {
    if (dsu.find(e.second.first) != dsu.find(e.second.second)) {
      dsu.unite(e.second.first, e.second.second);
      mst.push_back(e);
    }
  }

  return mst;
}

}  // namespace warmup
