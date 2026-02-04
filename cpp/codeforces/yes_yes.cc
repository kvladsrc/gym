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
    string s;
    std::cin >> s;

    string tmpl("Yes");
    int cur = -1;
    if (s[0] == 'Y') cur = 0;
    if (s[0] == 'e') cur = 1;
    if (s[0] == 's') cur = 2;
    if (cur == -1) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    bool match = true;
    for (size_t i = 1; i < s.size(); ++i) {
      if (s[i] != tmpl[(cur + i) % 3]) {
        match = false;
        break;
      }
    }

    if (match) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
