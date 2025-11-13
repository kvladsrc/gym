#include <cstddef>
#include <iostream>
#include <string>

using std::string;

// O(n)
int lex_compare(const string &a, const string &b) {
  for (size_t i = 0; i < a.size(); ++i) {
    if (std::tolower(a[i]) == std::tolower(b[i])) continue;

    if (std::tolower(a[i]) > std::tolower(b[i]))
      return 1;
    else
      return -1;
  }
  return 0;
}

int main(int argc, char *argv[]) {
  string a, b;
  std::cin >> a;
  std::cin >> b;
  std::cout << lex_compare(a, b) << "\n";
  return 0;
}
