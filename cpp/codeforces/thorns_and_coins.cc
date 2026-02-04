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
    int res = 0;
    bool last = true;
    for (int i = 1; i < n; ++i) {
      if (s[i] == '*' && s[i - 1] == '*') {
        last = false;
        break;
      }

      if (s[i - 1] == '@') res++;
    }

    if (last && s.back() == '@') res++;

    std::cout << res << "\n";
  }

  return 0;
}
