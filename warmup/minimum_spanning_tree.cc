#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int find_parent(vector<int> &parent, int i) {
  if (parent[i] == i) {
    return i;
  }

  return find_parent(parent, parent[i]);
}

void join(int a, int b, vector<int> &parent, vector<int> &rank) {
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

bool in_same(vector<int> &parent, int a, int b) {
  auto p_a = find_parent(parent, a);
  auto p_b = find_parent(parent, b);

  return p_a == p_b;
}

typedef pair<int, pair<int, int>> edge;

bool compare_pair(const edge &a, const edge &b) { return a.first < b.first; }

// Kruskal's algorithm on DSU. Assumes graph is connected.
int main(int argc, char *argv[]) {
  int v, e;
  std::cin >> v >> e;

  // Init DSU.
  vector<int> rank(v, 1);
  vector<int> parent(v);
  for (int i = 0; i < v; ++i) {
    parent[i] = i;
  }

  vector<edge> edges;
  while (e--) {
    int a, b, d;
    std::cin >> a >> b >> d;
    edges.push_back({d, {a, b}});
  }

  sort(edges.begin(), edges.end(), compare_pair);

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
