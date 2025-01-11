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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using graph = vector<set<size_t>>;

static size_t c_to_i(char c) { return c - 'a'; }

void dfs(size_t start, graph &g, vector<bool> &visited) {
  if (visited[start]) {
    return;
  }

  visited[start] = true;
  for (auto n : g[start]) {
    dfs(n, g, visited);
  }
}

bool reachable(size_t a, size_t b, graph &g) {
  vector<bool> visited(26, false);
  dfs(a, g, visited);
  return visited[b];
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<string> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  // Lets build a directed graph where each vertex is a char. Then we
  // extract facts from the list to build edges. x -> y means exists
  // two elements in the list a_i and a_i+1, a_i = ....x..., a_i+1 =
  // ....y.... (x appearce before the y).
  //
  // Then result is the list of chars in topological order. Chars
  // without edges can be added at any position, lets add them after.
  //
  // UPD. Because of alphabet is small we can use simple brute-force
  // by modifiyng bubble-sort algorithm: for every a_i > 0, a_i >
  // a_i-1 <=> a_i-1 is reacable from a_i, but not opposite. If we got
  // loop then solution does not exists.

  graph g(26);
  bool err = false;

  for (size_t idx = 1; idx < n; ++idx) {
    for (size_t char_pos = 0; char_pos < min(a[idx].size(), a[idx - 1].size());
         ++char_pos) {
      if (a[idx][char_pos] != a[idx - 1][char_pos]) {
        auto x = c_to_i(a[idx - 1][char_pos]);
        auto y = c_to_i(a[idx][char_pos]);
        g[x].insert(y);
        break;
      }

      if (char_pos == a[idx].size() - 1 && a[idx - 1].size() > a[idx].size()) {
        err = true;
        break;
      }
    }
  }

  vector<size_t> res(26);
  for (size_t idx = 0; idx < 26; ++idx) {
    res[idx] = idx;
  }

  while (true) {
    bool swapped = false;

    for (size_t a = 0; a < 26; ++a) {
      for (size_t b = a + 1; b < 26; ++b) {
        auto ba = reachable(res[b], res[a], g);
        auto ab = reachable(res[a], res[b], g);
        if (ab && ba) {
          err = true;
          break;
        }

        if (ba) {
          std::swap(res[a], res[b]);
          swapped = true;
        }
      }
    }

    if (err || !swapped) {
      break;
    }
  }

  if (err) {
    std::cout << "Impossible" << "\n";
  } else {
    for (auto c : res) {
      std::cout << char(c + 'a');
    }
    std::cout << "\n";
  }

  return 0;
}
