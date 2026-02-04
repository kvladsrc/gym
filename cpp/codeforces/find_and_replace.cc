#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string s;
    std::cin >> s;

    // 0: unset, 1: 0, 2: 1.
    unordered_map<char, int> m;
    bool success = true;
    m[s[0]] = 1;
    for (int i = 1; i < n; ++i) {
      if (m[s[i]] == 0) {
        if (m[s[i - 1]] == 1) {
          m[s[i]] = 2;
        } else {
          m[s[i]] = 1;
        }
        continue;
      }

      if (m[s[i]] == m[s[i - 1]]) {
        success = false;
        break;
      }
    }

    if (success) {
      std::cout << "YES\n";
    } else {
      std::cout << "NO\n";
    }
  }

  return 0;
}
