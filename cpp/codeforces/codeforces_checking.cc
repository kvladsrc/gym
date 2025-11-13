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

bool in_word(const string &w, char l) {
  return std::any_of(w.begin(), w.end(), [l](char c) { return c == l; });
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    char l;
    std::cin >> l;
    if (in_word("codeforces", l)) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
