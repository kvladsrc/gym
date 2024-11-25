#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
    bool same = true;
    string a, b;
    std::cin >> a;
    std::cin >> b;
    for (int i = 0; i < n; ++i) {
      if (a[i] == 'R' && b[i] != 'R') {
        same = false;
        continue;
      }

      if (b[i] == 'R' && a[i] != 'R') {
        same = false;
        continue;
      }
    }

    if (same) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }
  return 0;
}
