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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string res;
    for (int i = 0; i < 8; ++i) {
      for (int j = 0; j < 8; ++j) {
        char buf;
        std::cin >> buf;
        if (buf != '.') res += buf;
      }
    }
    std::cout << res << "\n";
  }

  return 0;
}
