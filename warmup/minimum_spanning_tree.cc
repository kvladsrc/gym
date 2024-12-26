#include <boost/range/algorithm/sort.hpp>
#include <iostream>
#include <string>
#include <vector>

using std::pair;
using std::string;
using std::vector;

static int find_parent(vector<int> &parent, int i) {
  if (parent[i] == i) {
    return i;
  }

  return find_parent(parent, parent[i]);
}

static void join(int a, int b, vector<int> &parent, vector<int> &rank) {
  auto p_a = find_parent(parent, a);
  auto p_b = find_parent(parent, b);

  if (p_a == p_b) {
    return;
  }

  if (rank[p_a] < rank[p_b]) {
    parent[p_a] = p_b;
  } else if (rank[p_a] > rank[p_b]) {
    parent[p_b] = p_a;
  } else {
    parent[p_b] = p_a;
    rank[p_a]++;
  }
}

static bool in_same(vector<int> &parent, int a, int b) {
  auto p_a = find_parent(parent, a);
  auto p_b = find_parent(parent, b);

  return p_a == p_b;
}

using edge = pair<int, pair<int, int>>;

static bool compare_pair(const edge &a, const edge &b) {
  return a.first < b.first;
}

// Kruskal's algorithm on DSU. Assumes graph is connected.
int main(int /*argc*/, char * /*argv*/[]) {
  int v = 0;
  int e = 0;
  std::cin >> v >> e;

  // Init DSU.
  vector<int> rank(v, 1);
  vector<int> parent(v);
  for (int i = 0; i < v; ++i) {
    parent[i] = i;
  }

  vector<edge> edges;
  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    int d = 0;
    std::cin >> a >> b >> d;
    edges.push_back({d, {a, b}});
  }

  boost::range::sort(edges, compare_pair);

  vector<edge> mst;
  for (auto e : edges) {
    if (!in_same(parent, e.second.first, e.second.second)) {
      mst.push_back(e);
      join(e.second.first, e.second.second, parent, rank);
    }
  }

  for (auto e : mst) {
    std::cout << e.second.first << " " << e.second.second << "\n";
  }

  return 0;
}
