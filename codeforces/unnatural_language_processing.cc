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
    int n;
    std::cin >> n;

    string s;
    std::cin >> s;

    string res;
    for (int i = 0; i < n; ++i) {
      if (i > 0 && i < n - 1 && (s[i + 1] == 'a' || s[i + 1] == 'e')) {
        res += '.';
      }
      res += s[i];
    }

    std::cout << res << "\n";
  }

  return 0;
}
