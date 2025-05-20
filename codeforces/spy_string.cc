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

int dist(string& a, string& b) {
  int res = 0;
  for (size_t i = 0; i < a.size(); ++i) {
    if (a[i] != b[i]) {
      res++;
    }
  }
  return res;
}

bool check(string& val, vector<string>& strings) {
  for (auto& s : strings) {
    if (dist(s, val) > 1) {
      return false;
    }
  }
  return true;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    vector<string> strings(n);
    for (auto& s : strings) {
      std::cin >> s;
    }

    string res = "-1";
    bool done = false;
    for (int c = 0; c < m; ++c) {
      for (int i = 0; i < n; ++i) {
        for (int l = 0; l < 26; ++l) {
          auto s = strings[i];
          s[c] = 'a' + l;
          if (check(s, strings)) {
            res = s;
            done = true;
            break;
          }
        }
        if (done) {
          break;
        }
      }
      if (done) {
        break;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
