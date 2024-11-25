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
    unordered_map<int, int> m;
    int res = -1;
    while (n--) {
      int buf;
      std::cin >> buf;

      if (res != -1) continue;

      m[buf]++;
      if (m[buf] >= 3) {
        res = buf;
      }
    }
    std::cout << res << "\n";
  }
  return 0;
}
