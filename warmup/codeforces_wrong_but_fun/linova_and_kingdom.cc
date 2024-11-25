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

typedef vector<vector<int>> graph;

struct CityMeta {
  int id;
  int parent = 0;
  // Number of tourist cities on path to capital.
  int64_t positive_impact = 0;
  // Number of industrial cities which are cross the city traveling to
  // capital.
  int64_t negative_impact = 0;
  // Number of tourist cities in connected cities which are not on the
  // way to capital.
  int tourist_ahead = 0;

  int calculatePositive(const vector<CityMeta> &city_meta) {
    auto p = id;
    int dist_to_capital = 0;
    while (p != city_meta[p].parent) {
      dist_to_capital++;
      p = city_meta[p].parent;
    }
    return dist_to_capital;
  }
};

class CompareCities {
 public:
  bool operator()(const CityMeta &c1, const CityMeta &c2) const {
    return (c1.positive_impact - c1.negative_impact) <
           (c2.positive_impact - c2.negative_impact);
  }
};

int main(int argc, char *argv[]) {
  int n, k;
  std::cin >> n >> k;

  graph g(n);
  for (int i = 0; i < (n - 1); ++i) {
    int a, b;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  // Idea is to keep the list of leafs to find the best. After
  // operation reduce all parents impact by one and remove the
  // leaf. If parent become leaf (has no tourist cities) add parent to
  // list.
  //
  // Key idea is impact of leaf cities does not changes while picking
  // optimal city. Optimal city if always leaf city.

  // Using DFS to set a parent city.
  vector<CityMeta> city_meta(n);
  priority_queue<CityMeta, vector<CityMeta>, CompareCities> heap;
  vector<int> q;
  q.push_back(0);
  while (!q.empty()) {
    auto cur = q.back();
    q.pop_back();

    city_meta[cur].id = cur;
    if (cur != 0) {
      city_meta[cur].tourist_ahead = g[cur].size() - 1;
    } else {
      city_meta[cur].tourist_ahead = g[cur].size();
    }

    // We are on leaf city. Calculate impact and add to heap.
    if (city_meta[cur].tourist_ahead == 0) {
      city_meta[cur].positive_impact =
          city_meta[cur].calculatePositive(city_meta);
      heap.push(city_meta[cur]);
    }

    for (auto c : g[cur]) {
      if (c == city_meta[cur].parent) {
        continue;
      }

      city_meta[c].parent = cur;
      q.push_back(c);
    }
  }

  int64_t res = 0;
  while (k--) {
    auto best_city = heap.top();
    heap.pop();

    res += best_city.positive_impact - best_city.negative_impact;
    city_meta[best_city.id].negative_impact++;

    // Check, if the parent city become leaf.
    auto &p = city_meta[best_city.parent];
    p.tourist_ahead--;
    if (p.tourist_ahead == 0) {
      p.positive_impact = best_city.positive_impact - 1;
      for (auto c : g[p.id]) {
        if (c == p.parent) {
          continue;
        }
        p.negative_impact += city_meta[c].negative_impact;
      }

      heap.push(city_meta[best_city.parent]);
    }
  }

  std::cout << res << "\n";

  return 0;
}
