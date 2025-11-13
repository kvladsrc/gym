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
    string s;
    std::cin >> s;

    string res;
    int idx = -1;
    bool err = false;
    map<char, bool> m;

    for (auto c : s) {
      if (idx == -1) {
        res.push_back(c);
        m[c] = true;
        idx = 0;
        continue;
      }

      if (idx > 0 && res[idx - 1] == c) {
        idx--;
        continue;
      }

      if (idx < static_cast<int>(res.size()) - 1 && res[idx + 1] == c) {
        idx++;
        continue;
      }

      if (idx == 0 && m.count(c) == 0) {
        res = c + res;
        m[c] = true;
        continue;
      }

      if (idx == static_cast<int>(res.size() - 1) && m.count(c) == 0) {
        res += c;
        m[c] = true;
        idx++;
        continue;
      }

      err = true;
      break;
    }

    if (err) {
      std::cout << "NO" << "\n";
      continue;
    }

    for (int i = 0; i < 26; ++i) {
      auto c = 'a' + i;
      if (m.count(c) == 0) {
        res.push_back(c);
      }
    }

    std::cout << "YES" << "\n";
    std::cout << res << "\n";
  }

  return 0;
}
