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

bool check_border(const string& buf) {
  if (std::any_of(std::begin(buf), std::end(buf),
                  [&](auto c) { return c != '1'; })) {
    return false;
  }
  return true;
}

bool check_mid(const string& buf) {
  if (buf.front() != '1' || buf.back() != '1') {
    return false;
  }
  if (std::any_of(buf.begin() + 1, buf.end() - 1,
                  [](char c) { return c != '0'; })) {
    return false;
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
    std::cin >> n;

    string s;
    std::cin >> s;

    int l = 1;
    while ((l * l) < n) {
      l++;
    }

    if ((l * l) != n) {
      std::cout << "NO" << "\n";
      continue;
    }

    bool res = true;
    for (int i = 0; i < l; ++i) {
      string buf;
      for (int j = 0; j < l; ++j) {
        buf += s[j + (i * l)];
      }

      if (i == 0 || i == (l - 1)) {
        res = check_border(buf) & res;
      } else {
        res = check_mid(buf) & res;
      }

      if (!res) {
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
