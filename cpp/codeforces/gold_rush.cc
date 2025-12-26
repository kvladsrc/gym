#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    set<int> visited;
    queue<int> q;
    q.push(n);
    bool res = false;

    while (!q.empty()) {
      auto cur = q.front();
      q.pop();

      if (cur == m) {
        res = true;
        break;
      }

      if (cur % 3 != 0) {
        continue;
      }

      auto p = cur / 3;

      if (visited.count(p) == 0) {
        q.push(p);
        visited.insert(p);
      }

      if (visited.count(2 * p) == 0) {
        q.push(2 * p);
        visited.insert(2 * p);
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
