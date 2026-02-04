#include <algorithm>
#include <cstddef>
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;
    char c;
    std::cin >> c;

    bool res = false;
    for (size_t i = 0; i < s.size(); ++i) {
      if (s[i] == c && i % 2 == 0 && (s.size() - 1 - i) % 2 == 0) {
        res = true;
        break;
      }
    }

    if (res) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
