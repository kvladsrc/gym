#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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

    unordered_map<string, bool> m;
    for (int i = 1; i < n; ++i) {
      string buf;
      buf = s[i - 1];
      buf += s[i];
      m[buf] = true;
    }

    std::cout << m.size() << "\n";
  }

  return 0;
}
