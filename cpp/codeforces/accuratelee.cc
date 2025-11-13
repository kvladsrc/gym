#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string s;
    std::cin >> s;

    string res, begin, end;
    for (auto c : s) {
      if (c == '0') {
        begin += c;
      } else {
        break;
      }
    }

    for (int i = n - 1; i >= 0; --i) {
      if (s[i] == '1') {
        end += s[i];
      } else {
        break;
      }
    }

    res += begin;
    if (s.size() > (begin.size() + end.size())) {
      res += '0';
    }
    res += end;

    std::cout << res << "\n";
  }

  return 0;
}
