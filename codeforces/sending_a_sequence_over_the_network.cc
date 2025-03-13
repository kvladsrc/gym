#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    vector<size_t> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    if (n == 1) {
      std::cout << "NO" << "\n";
      continue;
    }

    /*
     * Lets keep track a set of possible prefix length. From our
     * perspective there are no difference between 3... and ...3, so
     * lets store them both as a 4 (len of the prefix). Then for every
     * position try to find a next position.
     *
     * Lets precalculate every possible <start, end> segment from 1 to
     * n and n downto 1 to reduce the calculations.
     */

    vector<set<size_t>> segments(n);

    for (size_t i = 0; i < n; ++i) {
      {
        auto l = i;
        auto r = a[l] + l;
        if (r < a.size()) {
          segments[l].insert(r);
        }
      }

      {
        auto r = i;
        if (a[r] <= r) {
          auto l = r - a[r];
          segments[l].insert(r);
        }
      }
    }

    map<size_t, bool> visited;
    queue<size_t> positions;

    for (auto r : segments[0]) {
      positions.push(r);
      visited[r] = true;
    }

    bool res = false;

    while (!positions.empty()) {
      auto cur = positions.front();
      positions.pop();

      if (cur == n - 1) {
        res = true;
        break;
      }

      for (auto r : segments[cur + 1]) {
        if (visited[r]) {
          continue;
        }

        visited[r] = true;
        positions.push(r);
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
