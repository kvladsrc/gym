#include <algorithm>
#include <cctype>
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
    int n;
    std::cin >> n;

    string sound;
    std::cin >> sound;

    string tmpl = "meow", res;
    res += tolower(sound[0]);
    for (size_t i = 1; i < sound.size(); ++i) {
      char pre = tolower(sound[i - 1]);
      char lower = tolower(sound[i]);
      if (pre == lower) continue;
      res += lower;
    }

    if (tmpl == res) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
