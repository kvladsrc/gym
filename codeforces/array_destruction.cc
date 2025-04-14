#include <sys/types.h>

#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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
    n *= 2;
    vector<int> a(n);
    map<int, int> m;
    for (auto &i : a) {
      std::cin >> i;
      m[i]++;
    }

    sort(a.begin(), a.end(), std::greater<int>());
    vector<pair<size_t, size_t>> seq;
    int init = 0;

    for (size_t idx = 1; idx < n; ++idx) {
      auto buf = m;
      buf[a[idx]]--;
      buf[a.front()]--;
      int cur = max(a[idx], a.front());
      seq.push_back({a[idx], a.front()});

      bool done = true;
      for (size_t i = 1; i < n; ++i) {
        if (buf[a[i]] == 0) {
          continue;
        }
        buf[a[i]]--;

        if (buf[cur - a[i]] == 0) {
          done = false;
          break;
        }
        buf[cur - a[i]]--;

        seq.push_back({a[i], cur - a[i]});
        cur = max(a[i], cur - a[i]);
      }

      if (!done) {
        seq.clear();
        init = 0;
      } else {
        init = a[idx] + a.front();
        break;
      }
    }

    if (init != 0) {
      std::cout << "YES" << "\n";
      std::cout << init << "\n";
      for (auto p : seq) {
        std::cout << p.first << " " << p.second << "\n";
      }
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
