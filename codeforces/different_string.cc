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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    bool changed = false;
    for (size_t i = 1; i < s.size(); ++i) {
      if (s[i] != s[i - 1]) {
        char buf = s[i];
        s[i] = s[i - 1];
        s[i - 1] = buf;
        changed = true;
        break;
      }
    }

    if (changed) {
      std::cout << "YES\n";
      std::cout << s << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
